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

namespace FoundationDB.Client.Tests
{

	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class KeySelectorFacts : FdbSimpleTest
	{

		[Test]
		public void Test_KeySelector_Create()
		{
			{ // fGE{...}
				var sel = KeySelector.FirstGreaterOrEqual(Key("Hello"));
				Log(sel);
				Assert.That(sel.Key, Is.EqualTo(Key("Hello")));
				Assert.That(sel.Offset, Is.EqualTo(1));
				Assert.That(sel.OrEqual, Is.False);
				Assert.That(sel.ToString(), Does.StartWith("fGE{").And.EndsWith("}"));
				var (key, orEqual, offset) = sel;
				Assert.That(key, Is.EqualTo(Key("Hello")));
				Assert.That(offset, Is.EqualTo(1));
				Assert.That(orEqual, Is.False);

			}
			{ // fGT{...}
				var sel = KeySelector.FirstGreaterThan(Key("Hello"));
				Log(sel);
				Assert.That(sel.Key, Is.EqualTo(Key("Hello")));
				Assert.That(sel.Offset, Is.EqualTo(1));
				Assert.That(sel.OrEqual, Is.True);
				Assert.That(sel.ToString(), Does.StartWith("fGT{").And.EndsWith("}"));
				var (key, orEqual, offset) = sel;
				Assert.That(key, Is.EqualTo(Key("Hello")));
				Assert.That(offset, Is.EqualTo(1));
				Assert.That(orEqual, Is.True);
			}
			{ // lLE{...}
				var sel = KeySelector.LastLessOrEqual(Key("Hello"));
				Log(sel);
				Assert.That(sel.Key, Is.EqualTo(Key("Hello")));
				Assert.That(sel.Offset, Is.EqualTo(0));
				Assert.That(sel.OrEqual, Is.True);
				Assert.That(sel.ToString(), Does.StartWith("lLE{").And.EndsWith("}"));
				var (key, orEqual, offset) = sel;
				Assert.That(key, Is.EqualTo(Key("Hello")));
				Assert.That(offset, Is.EqualTo(0));
				Assert.That(orEqual, Is.True);
			}
			{ // lLT{...}
				var sel = KeySelector.LastLessThan(Key("Hello"));
				Log(sel);
				Assert.That(sel.Key, Is.EqualTo(Key("Hello")));
				Assert.That(sel.Offset, Is.EqualTo(0));
				Assert.That(sel.OrEqual, Is.False);
				Assert.That(sel.ToString(), Does.StartWith("lLT{").And.EndsWith("}"));
				var (key, orEqual, offset) = sel;
				Assert.That(key, Is.EqualTo(Key("Hello")));
				Assert.That(offset, Is.EqualTo(0));
				Assert.That(orEqual, Is.False);
			}
			{ // custom offset
				var sel = new KeySelector(Key("Hello"), true, 123);
				Log(sel);
				Assert.That(sel.Key, Is.EqualTo(Key("Hello")));
				Assert.That(sel.Offset, Is.EqualTo(123));
				Assert.That(sel.OrEqual, Is.True);
				Assert.That(sel.ToString(), Does.StartWith("fGT{").And.EndsWith("} + 122"));
				var (key, orEqual, offset) = sel;
				Assert.That(key, Is.EqualTo(Key("Hello")));
				Assert.That(offset, Is.EqualTo(123));
				Assert.That(orEqual, Is.True);
			}
		}

		[Test]
		public void Test_KeySelector_Add_Offsets()
		{
			{ // KeySelector++
				var sel = KeySelector.FirstGreaterOrEqual(Key("Hello"));
				sel++;
				Log(sel);
				Assert.That(sel.Key, Is.EqualTo(Key("Hello")));
				Assert.That(sel.Offset, Is.EqualTo(2));
				Assert.That(sel.OrEqual, Is.False);
				Assert.That(sel.ToString(), Does.StartWith("fGE{").And.EndsWith("} + 1"));
			}
			{ // KeySelector--
				var sel = KeySelector.FirstGreaterOrEqual(Key("Hello"));
				sel--;
				Log(sel);
				Assert.That(sel.Key, Is.EqualTo(Key("Hello")));
				Assert.That(sel.Offset, Is.EqualTo(0));
				Assert.That(sel.OrEqual, Is.False);
				Assert.That(sel.ToString(), Does.StartWith("lLT{").And.EndsWith("}"));
			}
			{ // fGE{...} + offset
				var sel = KeySelector.FirstGreaterOrEqual(Key("Hello"));
				var next = sel + 123;
				Log(next);
				Assert.That(next.Key, Is.EqualTo(Key("Hello")));
				Assert.That(next.Offset, Is.EqualTo(124));
				Assert.That(next.OrEqual, Is.False);
				Assert.That(next.ToString(), Does.StartWith("fGE{").And.EndsWith("} + 123"));
			}
			{ // fGT{...} + offset
				var sel = KeySelector.FirstGreaterThan(Key("Hello"));
				var next = sel + 123;
				Log(next);
				Assert.That(next.Key, Is.EqualTo(Key("Hello")));
				Assert.That(next.Offset, Is.EqualTo(124));
				Assert.That(next.OrEqual, Is.True);
				Assert.That(next.ToString(), Does.StartWith("fGT{").And.EndsWith("} + 123"));
			}
			{ // fGE{...} - offset
				var sel = KeySelector.FirstGreaterOrEqual(Key("Hello"));
				var next = sel - 123;
				Log(next);
				Assert.That(next.Key, Is.EqualTo(Key("Hello")));
				Assert.That(next.Offset, Is.EqualTo(-122));
				Assert.That(next.OrEqual, Is.False);
				Assert.That(next.ToString(), Does.StartWith("lLT{").And.EndsWith("} - 122"));
			}
			{ // fGT{...} - offset
				var sel = KeySelector.FirstGreaterThan(Key("Hello"));
				var next = sel - 123;
				Log(next);
				Assert.That(next.Key, Is.EqualTo(Key("Hello")));
				Assert.That(next.Offset, Is.EqualTo(-122));
				Assert.That(next.OrEqual, Is.True);
				Assert.That(next.ToString(), Does.StartWith("lLE{").And.EndsWith("} - 122"));
			}

		}

		[Test]
		public void Test_KeySelectorPair_Create()
		{
			{
				// Create(KeySelector, KeySelector)
				var begin = KeySelector.LastLessThan(Key("Hello"));
				var end = KeySelector.FirstGreaterThan(Key("World"));
				var pair = KeySelectorPair.Create(begin, end);
				Log(pair);
				// must not change the selectors
				Assert.That(pair.Begin, Is.EqualTo(begin));
				Assert.That(pair.End, Is.EqualTo(end));
			}
			{
				// Create(KeyRange)
				var pair = KeySelectorPair.Create(KeyRange.Create(Key("Hello"), Key("World")));
				Log(pair);
				// must apply FIRST_GREATER_OR_EQUAL on both bounds
				Assert.That(pair.Begin, Is.EqualTo(KeySelector.FirstGreaterOrEqual(Key("Hello"))));
				Assert.That(pair.End, Is.EqualTo(KeySelector.FirstGreaterOrEqual(Key("World"))));
			}
			{
				// Create(Slice, Slice)
				var pair = KeySelectorPair.Create(Key("Hello"), Key("World"));
				Log(pair);
				// must apply FIRST_GREATER_OR_EQUAL on both bounds
				Assert.That(pair.Begin, Is.EqualTo(KeySelector.FirstGreaterOrEqual(Key("Hello"))));
				Assert.That(pair.End, Is.EqualTo(KeySelector.FirstGreaterOrEqual(Key("World"))));
			}
		}

		[Test]
		public void Test_KeySelectorPair_Deconstruct()
		{
			var (begin, end) = KeySelectorPair.Create(Key("Hello"), Key("World"));
			Assert.That(begin, Is.EqualTo(KeySelector.FirstGreaterOrEqual(Key("Hello"))));
			Assert.That(end, Is.EqualTo(KeySelector.FirstGreaterOrEqual(Key("World"))));
		}

		[Test]
		public void Test_KeySelectorPair_StartsWith()
		{
			var prefix = Pack(("Hello", "World"));
			var pair = KeySelectorPair.StartsWith(prefix);
			Log(pair);
			Assert.That(pair.Begin, Is.EqualTo(KeySelector.FirstGreaterOrEqual(prefix)));
			Assert.That(pair.End, Is.EqualTo(KeySelector.FirstGreaterOrEqual(FdbKey.Increment(prefix))));
		}

		[Test]
		public void Test_KeySelectorPair_Tail()
		{
			var prefix = Key("Hello", "World");
			var cursor = Key(123);
			var key = Key("Hello", "World", 123);
			Assume.That(key, Is.EqualTo(prefix + cursor));

			{ // orEqual: true
				var pair = KeySelectorPair.Tail(prefix, cursor, orEqual: true);
				Log(pair);
				Assert.That(pair.Begin, Is.EqualTo(KeySelector.FirstGreaterOrEqual(key)));
				Assert.That(pair.End, Is.EqualTo(KeySelector.FirstGreaterOrEqual(FdbKey.Increment(prefix))));
			}
			{ // orEqual: false
				var pair = KeySelectorPair.Tail(prefix, cursor, orEqual: false);
				Log(pair);
				Assert.That(pair.Begin, Is.EqualTo(KeySelector.FirstGreaterThan(key)));
				Assert.That(pair.End, Is.EqualTo(KeySelector.FirstGreaterOrEqual(FdbKey.Increment(prefix))));
			}
		}

		[Test]
		public void Test_KeySelectorPair_Head()
		{
			var prefix = Key("Hello", "World");
			var cursor = Key(123);
			var key = Key("Hello", "World", 123);
			Assume.That(key, Is.EqualTo(prefix + cursor));

			{ // orEqual: true
				var pair = KeySelectorPair.Head(prefix, cursor, orEqual: true);
				Log(pair);
				Assert.That(pair.Begin, Is.EqualTo(KeySelector.FirstGreaterThan(prefix)));
				Assert.That(pair.End, Is.EqualTo(KeySelector.FirstGreaterThan(key)));
			}
			{ // orEqual: false
				var pair = KeySelectorPair.Head(prefix, cursor, orEqual: false);
				Log(pair);
				Assert.That(pair.Begin, Is.EqualTo(KeySelector.FirstGreaterThan(prefix)));
				Assert.That(pair.End, Is.EqualTo(KeySelector.FirstGreaterOrEqual(key)));
			}
		}

	}

}
