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

// ReSharper disable SuspiciousTypeConversion.Global
#pragma warning disable CS0618 // Type or member is obsolete

namespace FoundationDB.Client.Tests
{
	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class FdbKeyRangeFacts : FdbSimpleTest
	{

		[Test]
		public void Test_FdbKeyRange_Single()
		{
			var subspace = GetSubspace(STuple.Create(42));

			{ // [from, from.\0[
				var range = FdbKeyRange.Single(subspace.Key(123));
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Inclusive));

				Assert.That(range.ToString(), Is.EqualTo("{ …(123,) <= k < …(123,).<00> }"));
				Assert.That(range.ToString("K"), Is.EqualTo("{ (42, 123) <= k < (42, 123).<00> }"));
				Assert.That(range.ToString("P"), Is.EqualTo("{ (42, 123) <= k < (42, 123).<00> }"));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0x00));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.ToBeginSelector(), Is.EqualTo(KeySelector.FirstGreaterOrEqual(TuPack.EncodeKey(42, 123))));
				Assert.That(range.ToEndSelector(), Is.EqualTo(KeySelector.FirstGreaterThan(TuPack.EncodeKey(42, 123))));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key(123)));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(123).Successor()));

				Assert.That(range.Contains(subspace.Key(122)), Is.False);
				Assert.That(range.Contains(subspace.Key(122).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123)), Is.True);
				Assert.That(range.Contains(subspace.Key(123).Successor()), Is.False);

				Assert.That(range.Contains(TuPack.EncodeKey(42, 122) + 0xFF), Is.False);
				Assert.That(range.Contains(TuPack.EncodeKey(42, 123)), Is.True);
				Assert.That(range.Contains(TuPack.EncodeKey(42, 122) + 0x00), Is.False);
			}
		}

		[Test]
		public void Test_FdbKeyRange_Subspace()
		{
			var subspace = GetSubspace(STuple.Create(42));

			{ // [prefix, prefix+1[
				FdbSubspaceKeyRange range = subspace.ToRange(inclusive: true);
				Log($"# {range}");
				Assert.That(range.Subspace, Is.SameAs(subspace));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(43)));

				Assert.That(range.ToString(), Is.EqualTo("{ (42,) <= k < (42,)+1 }"));
				Assert.That(range.ToString("K"), Is.EqualTo("{ (42,) <= k < (42,)+1 }"));
				Assert.That(range.ToString("P"), Is.EqualTo("{ (42,) <= k < (42,)+1 }"));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.ToBeginSelector(), Is.EqualTo(KeySelector.FirstGreaterOrEqual(TuPack.EncodeKey(42))));
				Assert.That(range.ToEndSelector(), Is.EqualTo(KeySelector.FirstGreaterOrEqual(TuPack.EncodeKey(43))));

				Assert.That(range.Contains(KeySubspace.Empty.Key(41)), Is.False);
				Assert.That(range.Contains(KeySubspace.Empty.Key(41).Last()), Is.False);
				Assert.That(range.Contains(KeySubspace.Empty.Key(42)), Is.True);
				Assert.That(range.Contains(KeySubspace.Empty.Key(42).Successor()), Is.True);
				Assert.That(range.Contains(KeySubspace.Empty.Key(42).Last()), Is.True);
				Assert.That(range.Contains(KeySubspace.Empty.Key(43)), Is.False);

				Assert.That(range.Contains(TuPack.EncodeKey(41) + 0xFF), Is.False);
				Assert.That(range.Contains(TuPack.EncodeKey(42)), Is.True);
				Assert.That(range.Contains(TuPack.EncodeKey(42) + 0x00), Is.True);
				Assert.That(range.Contains(TuPack.EncodeKey(42) + 0xFF), Is.True);
				Assert.That(range.Contains(TuPack.EncodeKey(43)), Is.False);
			}

			{ // [prefix.\0, prefix+1[
				FdbSubspaceKeyRange range = subspace.ToRange();
				Log($"# {range}");
				Assert.That(range.Subspace, Is.SameAs(subspace));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42) + 0x00));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(43)));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.ToBeginSelector(), Is.EqualTo(KeySelector.FirstGreaterThan(TuPack.EncodeKey(42))));
				Assert.That(range.ToEndSelector(), Is.EqualTo(KeySelector.FirstGreaterOrEqual(TuPack.EncodeKey(43))));

				Assert.That(range.Contains(KeySubspace.Empty.Key(41)), Is.False);
				Assert.That(range.Contains(KeySubspace.Empty.Key(41).Last()), Is.False);
				Assert.That(range.Contains(KeySubspace.Empty.Key(42)), Is.False);
				Assert.That(range.Contains(KeySubspace.Empty.Key(42).Successor()), Is.True);
				Assert.That(range.Contains(KeySubspace.Empty.Key(42).Last()), Is.True);
				Assert.That(range.Contains(KeySubspace.Empty.Key(43)), Is.False);

				Assert.That(range.Contains(TuPack.EncodeKey(41) + 0xFF), Is.False);
				Assert.That(range.Contains(TuPack.EncodeKey(42)), Is.False);
				Assert.That(range.Contains(TuPack.EncodeKey(42) + 0x00), Is.True);
				Assert.That(range.Contains(TuPack.EncodeKey(42) + 0xFF), Is.True);
				Assert.That(range.Contains(TuPack.EncodeKey(43)), Is.False);
			}
		}

		[Test]
		public void Test_FdbKeyRange_RawKey()
		{
			{ // [from, to[
				FdbRawKeyRange range = FdbKeyRange.Between(TuPack.EncodeKey(42, 123), TuPack.EncodeKey(42, 456));
				Log($"# {range}");
				Log($"> lower: {range.Begin}");
				Log($"> upper: {range.End}");
				Assert.That(range.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123)));
				Assert.That(range.End, Is.EqualTo(TuPack.EncodeKey(42, 456)));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 456)));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.ToBeginSelector(), Is.EqualTo(KeySelector.FirstGreaterOrEqual(TuPack.EncodeKey(42, 123))));
				Assert.That(range.ToEndSelector(), Is.EqualTo(KeySelector.FirstGreaterOrEqual(TuPack.EncodeKey(42, 456))));

				Assert.That(range.Contains(TuPack.EncodeKey(42, 122) + 0xFF), Is.False);
				Assert.That(range.Contains(TuPack.EncodeKey(42, 123)), Is.True);
				Assert.That(range.Contains(TuPack.EncodeKey(42, 123) + 0x00), Is.True);
				Assert.That(range.Contains(TuPack.EncodeKey(42, 123, "hello")), Is.True);
				Assert.That(range.Contains(TuPack.EncodeKey(42, 124)), Is.True);
				Assert.That(range.Contains(TuPack.EncodeKey(42, 455)), Is.True);
				Assert.That(range.Contains(TuPack.EncodeKey(42, 455, "hello")), Is.True);
				Assert.That(range.Contains(TuPack.EncodeKey(42, 456)), Is.False);
				Assert.That(range.Contains(TuPack.EncodeKey(42, 456) + 0x00), Is.False);
				Assert.That(range.Contains(TuPack.EncodeKey(42, 456) + 0xFF), Is.False);
				Assert.That(range.Contains(TuPack.EncodeKey(42, 457)), Is.False);
			}
		}

		[Test]
		public void Test_FdbKeyRange_Between()
		{
			var subspace = GetSubspace(STuple.Create(42));

			{ // [from, to[
				var range = FdbKeyRange.Between(subspace.Key(123), subspace.Key(456));
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(456)));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Exclusive));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 456)));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.ToBeginSelector(), Is.EqualTo(KeySelector.FirstGreaterOrEqual(TuPack.EncodeKey(42, 123))));
				Assert.That(range.ToEndSelector(), Is.EqualTo(KeySelector.FirstGreaterOrEqual(TuPack.EncodeKey(42, 456))));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin().FastEqualTo(subspace.Key(123)));
				Assert.That(range.Begin().Equals(subspace.Key(123)));
				Assert.That(range.Begin().Equals((object) subspace.Key(123)));
				Assert.That(subspace.Key(123), Is.EqualTo(range.Begin()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key(123)));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End().FastEqualTo(subspace.Key(456)));
				Assert.That(subspace.Key(456), Is.EqualTo(range.End()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(456)));

				Assert.That(range.Contains(subspace.Key(122).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123)), Is.True);
				Assert.That(range.Contains(subspace.Key(123).Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "hello")), Is.True);
				Assert.That(range.Contains(subspace.Key(124)), Is.True);
				Assert.That(range.Contains(subspace.Key(455)), Is.True);
				Assert.That(range.Contains(subspace.Key(455, "hello")), Is.True);
				Assert.That(range.Contains(subspace.Key(456)), Is.False);
				Assert.That(range.Contains(subspace.Key(456).Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key(456).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(457)), Is.False);
			}
			Log();

			{ // ]from, to[
				var range = FdbKeyRange.Between(subspace.Key(123), KeyRangeMode.Exclusive, subspace.Key(456), KeyRangeMode.Exclusive);
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Exclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(456)));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Exclusive));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0x00));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 456)));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.ToBeginSelector(), Is.EqualTo(KeySelector.FirstGreaterThan(TuPack.EncodeKey(42, 123))));
				Assert.That(range.ToEndSelector(), Is.EqualTo(KeySelector.FirstGreaterOrEqual(TuPack.EncodeKey(42, 456))));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin().Equals(subspace.Key(123).Successor()));
				Assert.That(range.Begin().Equals((IFdbKey) subspace.Key(123).Successor()));
				Assert.That(range.Begin().Equals((object) subspace.Key(123).Successor()));
				Assert.That(range.Begin().FastEqualTo(subspace.Key(123).Successor()));
				Assert.That(subspace.Key(123).Successor(), Is.EqualTo(range.Begin()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key(123).Successor()));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End().FastEqualTo(subspace.Key(456)));
				Assert.That(subspace.Key(456), Is.EqualTo(range.End()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(456)));

				Assert.That(range.Contains(subspace.Key(122).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123)), Is.False);
				Assert.That(range.Contains(subspace.Key(123).Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "hello")), Is.True);
				Assert.That(range.Contains(subspace.Key(124)), Is.True);
				Assert.That(range.Contains(subspace.Key(455)), Is.True);
				Assert.That(range.Contains(subspace.Key(455, "hello")), Is.True);
				Assert.That(range.Contains(subspace.Key(456)), Is.False);
				Assert.That(range.Contains(subspace.Key(456).Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key(456).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(457)), Is.False);
			}
			Log();

			{ // [from, to+1[
				var range = FdbKeyRange.Between(subspace.Key(123), KeyRangeMode.Inclusive, subspace.Key(456), KeyRangeMode.NextSibling);
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(456)));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.NextSibling));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 457)));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.ToBeginSelector(), Is.EqualTo(KeySelector.FirstGreaterOrEqual(TuPack.EncodeKey(42, 123))));
				Assert.That(range.ToEndSelector(), Is.EqualTo(KeySelector.FirstGreaterOrEqual(TuPack.EncodeKey(42, 457))));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key(123)));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End().FastEqualTo(subspace.Key(457)));
				Assert.That(subspace.Key(457), Is.EqualTo(range.End()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(457)));

				Assert.That(range.Contains(subspace.Key(122).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123)), Is.True);
				Assert.That(range.Contains(subspace.Key(123).Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "hello")), Is.True);
				Assert.That(range.Contains(subspace.Key(124)), Is.True);
				Assert.That(range.Contains(subspace.Key(455)), Is.True);
				Assert.That(range.Contains(subspace.Key(455, "hello")), Is.True);
				Assert.That(range.Contains(subspace.Key(456)), Is.True);
				Assert.That(range.Contains(subspace.Key(456).Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(456).Last()), Is.True);
				Assert.That(range.Contains(subspace.Key(457)), Is.False);
			}
			Log();

			{ // [(from,*), (to,*)]
				var range = FdbKeyRange.Between(subspace.Key(123), KeyRangeMode.Exclusive, subspace.Key(456), KeyRangeMode.Last);
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Exclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(456)));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0x00));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 456) + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.ToBeginSelector(), Is.EqualTo(KeySelector.FirstGreaterThan(TuPack.EncodeKey(42, 123))));
				Assert.That(range.ToEndSelector(), Is.EqualTo(KeySelector.FirstGreaterOrEqual(TuPack.EncodeKey(42, 456) + 0xFF)));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key(123).Successor()));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(456).Last()));

				Assert.That(range.Contains(subspace.Key(122).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123)), Is.False);
				Assert.That(range.Contains(subspace.Key(123).Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "hello")), Is.True);
				Assert.That(range.Contains(subspace.Key(124)), Is.True);
				Assert.That(range.Contains(subspace.Key(455)), Is.True);
				Assert.That(range.Contains(subspace.Key(455, "hello")), Is.True);
				Assert.That(range.Contains(subspace.Key(456)), Is.True);
				Assert.That(range.Contains(subspace.Key(456).Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(456).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(457)), Is.False);
			}
			Log();

		}

		[Test]
		public void Test_FdbKeyRange_Key_ToHeadRange()
		{
			var subspace = GetSubspace(STuple.Create(42));

			{ // TKey + (T1,)
				var range = subspace.Key(123).ToHeadRange("foo");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123, "foo")));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Exclusive));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo")));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key(123)));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(123, "foo")));
			}
			Log();

			{ // TKey + (T1, T2)
				var range = subspace.Key(123).ToHeadRange("foo", "bar");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123, "foo", "bar")));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Exclusive));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo", "bar")));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key(123)));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(123, "foo", "bar")));
			}
			Log();

			{ // TKey + (T1, T2, T3)
				var range = subspace.Key(123).ToHeadRange("foo", "bar", "baz");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123, "foo", "bar", "baz")));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Exclusive));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo", "bar", "baz")));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key(123)));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(123, "foo", "bar", "baz")));
			}
			Log();
		}

		[Test]
		public void Test_FdbKeyRange_Subspace_ToHeadRange()
		{
			var subspace = GetSubspace(STuple.Create(42));

			{ // IKeySubspace + (T1,)
				var range = subspace.ToHeadRange(123);
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key()));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Exclusive));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123)));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key()));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(123)));
			}
			Log();

			{ // IKeySubspace + (T1, T2)
				var range = subspace.ToHeadRange(123, "foo");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key()));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123, "foo")));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Exclusive));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo")));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key()));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(123, "foo")));
			}
			Log();

			{ // IKeySubspace + (T1, T2, T3)
				var range = subspace.ToHeadRange(123, "foo", "bar");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key()));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123, "foo", "bar")));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Exclusive));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo", "bar")));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key()));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(123, "foo", "bar")));
			}
			Log();
		}

		[Test]
		public void Test_FdbKeyRange_Key_ToHeadRangeInclusive()
		{
			var subspace = GetSubspace(STuple.Create(42));

			{ // T1
				var range = subspace.Key(123).ToHeadRangeInclusive("foo");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123, "foo")));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo") + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key(123)));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(123, "foo").Last()));

				Assert.That(range.Contains(subspace.Key()), Is.False);
				Assert.That(range.Contains(subspace.Key().Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key(0)), Is.False);
				// start of the range
				Assert.That(range.Contains(subspace.Key(123)), Is.True);
				Assert.That(range.Contains(subspace.Key(123).Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "zzz")), Is.True);
				// end of the range
				Assert.That(range.Contains(subspace.Key(123, "foo").Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "fop")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "zzz")), Is.False);
				Assert.That(range.Contains(subspace.Key(123).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(124)), Is.False);
			}
			Log();

			{ // T2
				var range = subspace.Key(123).ToHeadRangeInclusive("foo", "bar");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123, "foo", "bar")));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo", "bar") + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key(123)));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(123, "foo", "bar").Last()));

				Assert.That(range.Contains(subspace.Key()), Is.False);
				Assert.That(range.Contains(subspace.Key().Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key(0)), Is.False);
				// start of the range
				Assert.That(range.Contains(subspace.Key(123)), Is.True);
				Assert.That(range.Contains(subspace.Key(123).Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "zzz")), Is.True);
				// end of the range
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar").Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bas")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo").Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "fop")), Is.False);
				Assert.That(range.Contains(subspace.Key(123).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(124)), Is.False);
			}
			Log();

			{ // T3
				var range = subspace.Key(123).ToHeadRangeInclusive("foo", "bar", "baz");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123, "foo", "bar", "baz")));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo", "bar", "baz") + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key(123)));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(123, "foo", "bar", "baz").Last()));

				Assert.That(range.Contains(subspace.Key()), Is.False);
				Assert.That(range.Contains(subspace.Key().Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key(0)), Is.False);
				// start of the range
				Assert.That(range.Contains(subspace.Key(123)), Is.True);
				Assert.That(range.Contains(subspace.Key(123).Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz", "zzz")), Is.True);
				// end of the range
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz").Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "ba{")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar").Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bas")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo").Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "fop")), Is.False);
				Assert.That(range.Contains(subspace.Key(123).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(124)), Is.False);
			}
			Log();

		}

		[Test]
		public void Test_FdbKeyRange_Subspace_ToHeadRangeInclusive()
		{
			var subspace = GetSubspace(STuple.Create(42));

			{ // T1
				var range = subspace.ToHeadRangeInclusive(123);
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key()));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key()));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(123).Last()));

				Assert.That(range.Contains(subspace.Key()), Is.True);
				Assert.That(range.Contains(subspace.Key().Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(0)), Is.True);
				Assert.That(range.Contains(subspace.Key(123)), Is.True);
				Assert.That(range.Contains(subspace.Key(123).Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, 456)), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "fop")), Is.True);
				Assert.That(range.Contains(subspace.Key(123).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(124)), Is.False);
			}
			Log();

			{ // T2
				var range = subspace.ToHeadRangeInclusive(123, "foo");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key()));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123, "foo")));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo") + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key()));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(123, "foo").Last()));

				Assert.That(range.Contains(subspace.Key()), Is.True);
				Assert.That(range.Contains(subspace.Key().Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(0)), Is.True);
				Assert.That(range.Contains(subspace.Key(123)), Is.True);
				Assert.That(range.Contains(subspace.Key(123).Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "zzz")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo").Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "fop")), Is.False);
				Assert.That(range.Contains(subspace.Key(123).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(124)), Is.False);
			}
			Log();

			{ // T3
				var range = subspace.ToHeadRangeInclusive(123, "foo", "bar");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key()));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123, "foo", "bar")));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42)));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo", "bar") + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));

				Assert.That(range.Begin(), Is.EqualTo(range.ToBeginKey()));
				Assert.That(range.Begin(), Is.EqualTo(subspace.Key()));

				Assert.That(range.End(), Is.EqualTo(range.ToEndKey()));
				Assert.That(range.End(), Is.EqualTo(subspace.Key(123, "foo", "bar").Last()));

				Assert.That(range.Contains(subspace.Key()), Is.True);
				Assert.That(range.Contains(subspace.Key().Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(0)), Is.True);
				Assert.That(range.Contains(subspace.Key(123)), Is.True);
				Assert.That(range.Contains(subspace.Key(123).Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "zzz")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar").Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo").Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "fop")), Is.False);
				Assert.That(range.Contains(subspace.Key(123).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(124)), Is.False);
			}
			Log();

		}

		[Test]
		public void Test_FdbKeyRange_Key_ToTailRange()
		{
			var subspace = GetSubspace(STuple.Create(42));

			{ // T1
				var range = subspace.Key(123).ToTailRange("foo");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123, "foo")));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo")));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));
				
				Assert.That(range.Contains(subspace.Key()), Is.False);
				Assert.That(range.Contains(subspace.Key().Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key(0)), Is.False);
				Assert.That(range.Contains(subspace.Key(123)), Is.False);
				Assert.That(range.Contains(subspace.Key(123).Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "fon")), Is.False);
				// start of the range
				Assert.That(range.Contains(subspace.Key(123, "foo")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz", "zzz")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "ba{")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bas")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "fop")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "zzz")), Is.True);
				// end of the range
				Assert.That(range.Contains(subspace.Key(123).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(124)), Is.False);
			}
			Log();

			{ // T2
				var range = subspace.Key(123).ToTailRange("foo", "bar");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123, "foo", "bar")));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo", "bar")));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));
				
				Assert.That(range.Contains(subspace.Key()), Is.False);
				Assert.That(range.Contains(subspace.Key().Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key(0)), Is.False);
				Assert.That(range.Contains(subspace.Key(123)), Is.False);
				Assert.That(range.Contains(subspace.Key(123).Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo").Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "baq")), Is.False);
				// start of the range
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz", "zzz")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "ba{")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bas")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "fop")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "zzz")), Is.True);
				// end of the range
				Assert.That(range.Contains(subspace.Key(123).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(124)), Is.False);
			}
			Log();

			{ // T3
				var range = subspace.Key(123).ToTailRange("foo", "bar", "baz");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123, "foo", "bar", "baz")));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo", "bar", "baz")));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));
				
				Assert.That(range.Contains(subspace.Key()), Is.False);
				Assert.That(range.Contains(subspace.Key().Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key(0)), Is.False);
				Assert.That(range.Contains(subspace.Key(123)), Is.False);
				Assert.That(range.Contains(subspace.Key(123).Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo").Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar").Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "bay")), Is.False);
				// start of the range
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz", "zzz")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "ba{")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bas")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "fop")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "zzz")), Is.True);
				// end of the range
				Assert.That(range.Contains(subspace.Key(123).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(124)), Is.False);
			}
			Log();

		}

		[Test]
		public void Test_FdbKeyRange_Subspace_ToTailRange()
		{
			var subspace = GetSubspace(STuple.Create(42, 123));

			{ // T1
				var range = subspace.ToTailRange("foo");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key("foo")));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key()));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo")));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));
				
				Assert.That(range.Contains(subspace.Key()), Is.False);
				Assert.That(range.Contains(subspace.Key().Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key("fon")), Is.False);
				// start of the range
				Assert.That(range.Contains(subspace.Key("foo")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz", "zzz")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "ba{")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bas")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key("fop")), Is.True);
				Assert.That(range.Contains(subspace.Key("zzz")), Is.True);
				// end of the range
				Assert.That(range.Contains(subspace.Last()), Is.False);
				Assert.That(range.Contains(subspace.NextSibling()), Is.False);
			}
			Log();

			{ // T2
				var range = subspace.ToTailRange("foo", "bar");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key("foo", "bar")));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key()));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo", "bar")));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));
				
				Assert.That(range.Contains(subspace.Key()), Is.False);
				Assert.That(range.Contains(subspace.Key().Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key("foo")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo").Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "baq")), Is.False);
				// start of the range
				Assert.That(range.Contains(subspace.Key("foo", "bar")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz", "zzz")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "ba{")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bas")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key("fop")), Is.True);
				Assert.That(range.Contains(subspace.Key("zzz")), Is.True);
				// end of the range
				Assert.That(range.Contains(subspace.Last()), Is.False);
				Assert.That(range.Contains(subspace.NextSibling()), Is.False);
			}
			Log();

			{ // T3
				var range = subspace.ToTailRange("foo", "bar", "baz");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key("foo", "bar", "baz")));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.Inclusive));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key()));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(TuPack.EncodeKey(42, 123, "foo", "bar", "baz")));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));
				
				Assert.That(range.Contains(subspace.Key()), Is.False);
				Assert.That(range.Contains(subspace.Key().Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key("foo")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo").Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar").Successor()), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "bay")), Is.False);
				// start of the range
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz").Successor()), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz", "zzz")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "ba{")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bas")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key("fop")), Is.True);
				Assert.That(range.Contains(subspace.Key("zzz")), Is.True);
				// end of the range
				Assert.That(range.Contains(subspace.Last()), Is.False);
				Assert.That(range.Contains(subspace.NextSibling()), Is.False);
			}
			Log();

		}

		[Test]
		public void Test_FdbKeyRange_Key_ToTailRangeExclusive()
		{
			var subspace = GetSubspace(STuple.Create(42));

			{ // T1
				var range = subspace.Key(123).ToTailRangeExclusive("foo");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123, "foo")));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.NextSibling));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(FdbKey.Increment(TuPack.EncodeKey(42, 123, "foo"))));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));
				
				Assert.That(range.Contains(subspace.Key()), Is.False);
				Assert.That(range.Contains(subspace.Key(123)), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz")), Is.False); // excluded!
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz", "zzz")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz").Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "ba{")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "zzz")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar").Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bas")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "zzz")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo").Last()), Is.False);
				// start of the range
				Assert.That(range.Contains(subspace.Key(123, "fop")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "zzz")), Is.True);
				// end of the range
				Assert.That(range.Contains(subspace.Key(123).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(124)), Is.False);
			}
			Log();

			{ // T2
				var range = subspace.Key(123).ToTailRangeExclusive("foo", "bar");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123, "foo", "bar")));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.NextSibling));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(FdbKey.Increment(TuPack.EncodeKey(42, 123, "foo", "bar"))));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));
				
				Assert.That(range.Contains(subspace.Key()), Is.False);
				Assert.That(range.Contains(subspace.Key(123)), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz")), Is.False); // excluded!
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz", "zzz")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz").Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "ba{")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "zzz")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar").Last()), Is.False);
				// start of the range
				Assert.That(range.Contains(subspace.Key(123, "foo", "bas")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "zzz")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "fop")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "zzz")), Is.True);
				// end of the range
				Assert.That(range.Contains(subspace.Key(123).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(124)), Is.False);
			}
			Log();

			{ // T3
				var range = subspace.Key(123).ToTailRangeExclusive("foo", "bar", "baz");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key(123, "foo", "bar", "baz")));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.NextSibling));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key(123)));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(FdbKey.Increment(TuPack.EncodeKey(42, 123, "foo", "bar", "baz"))));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));
				
				Assert.That(range.Contains(subspace.Key()), Is.False);
				Assert.That(range.Contains(subspace.Key(123)), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz")), Is.False); // excluded!
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz", "zzz")), Is.False);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "baz").Last()), Is.False);
				// start of the range
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "ba{")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar", "zzz")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bar").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "bas")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo", "zzz")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "foo").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "fop")), Is.True);
				Assert.That(range.Contains(subspace.Key(123, "zzz")), Is.True);
				// end of the range
				Assert.That(range.Contains(subspace.Key(123).Last()), Is.False);
				Assert.That(range.Contains(subspace.Key(124)), Is.False);
			}
			Log();

		}

		[Test]
		public void Test_FdbKeyRange_Subspace_ToTailRangeExclusive()
		{
			var subspace = GetSubspace(STuple.Create(42, 123));

			{ // T1
				var range = subspace.ToTailRangeExclusive("foo");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key("foo")));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.NextSibling));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key()));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(FdbKey.Increment(TuPack.EncodeKey(42, 123, "foo"))));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));
				
				Assert.That(range.Contains(subspace.Key()), Is.False);
				Assert.That(range.Contains(subspace.Key("foo")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz")), Is.False); // excluded!
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz", "zzz")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz").Last()), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "ba{")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "zzz")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar").Last()), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bas")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "zzz")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo").Last()), Is.False);
				// start of the range
				Assert.That(range.Contains(subspace.Key("fop")), Is.True);
				Assert.That(range.Contains(subspace.Key("zzz")), Is.True);
				// end of the range
				Assert.That(range.Contains(subspace.Last()), Is.False);
				Assert.That(range.Contains(subspace.NextSibling()), Is.False);
			}
			Log();

			{ // T2
				var range = subspace.ToTailRangeExclusive("foo", "bar");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key("foo", "bar")));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.NextSibling));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key()));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(FdbKey.Increment(TuPack.EncodeKey(42, 123, "foo", "bar"))));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));
				
				Assert.That(range.Contains(subspace.Key()), Is.False);
				Assert.That(range.Contains(subspace.Key("foo")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz")), Is.False); // excluded!
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz", "zzz")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz").Last()), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "ba{")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "zzz")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar").Last()), Is.False);
				// start of the range
				Assert.That(range.Contains(subspace.Key("foo", "bas")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "zzz")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key("fop")), Is.True);
				Assert.That(range.Contains(subspace.Key("zzz")), Is.True);
				// end of the range
				Assert.That(range.Contains(subspace.Last()), Is.False);
				Assert.That(range.Contains(subspace.NextSibling()), Is.False);
			}
			Log();

			{ // T3
				var range = subspace.ToTailRangeExclusive("foo", "bar", "baz");
				Log($"# {range}");
				Log($"> lower: {range.LowerKey} ({range.LowerMode})");
				Log($"> upper: {range.UpperKey} ({range.UpperMode})");
				Assert.That(range.LowerKey, Is.EqualTo(subspace.Key("foo", "bar", "baz")));
				Assert.That(range.LowerMode, Is.EqualTo(KeyRangeMode.NextSibling));
				Assert.That(range.UpperKey, Is.EqualTo(subspace.Key()));
				Assert.That(range.UpperMode, Is.EqualTo(KeyRangeMode.Last));

				var kr = range.ToKeyRange();
				Log($"> range: {kr}");
				Log($"  - begin: {kr.Begin:X}");
				Log($"  - end  : {kr.End:X}");
				Assert.That(kr.Begin, Is.EqualTo(FdbKey.Increment(TuPack.EncodeKey(42, 123, "foo", "bar", "baz"))));
				Assert.That(kr.End, Is.EqualTo(TuPack.EncodeKey(42, 123) + 0xFF));

				Assert.That(range.ToBeginKey(), Is.EqualTo(kr.Begin));
				Assert.That(range.ToEndKey(), Is.EqualTo(kr.End));
				
				Assert.That(range.Contains(subspace.Key()), Is.False);
				Assert.That(range.Contains(subspace.Key("foo")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz")), Is.False); // excluded!
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz", "zzz")), Is.False);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "baz").Last()), Is.False);
				// start of the range
				Assert.That(range.Contains(subspace.Key("foo", "bar", "ba{")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar", "zzz")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bar").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "bas")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo", "zzz")), Is.True);
				Assert.That(range.Contains(subspace.Key("foo").Last()), Is.True);
				Assert.That(range.Contains(subspace.Key("fop")), Is.True);
				Assert.That(range.Contains(subspace.Key("zzz")), Is.True);
				// end of the range
				Assert.That(range.Contains(subspace.Last()), Is.False);
				Assert.That(range.Contains(subspace.NextSibling()), Is.False);
			}
			Log();

		}

	}

}
