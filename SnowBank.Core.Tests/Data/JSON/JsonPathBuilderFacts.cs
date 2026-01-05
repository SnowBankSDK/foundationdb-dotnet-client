#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

// ReSharper disable GrammarMistakeInComment

namespace SnowBank.Data.Json.Tests
{

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	public sealed class JsonPathBuilderFacts : SimpleTest
	{

		[Test]
		public void Test_JsonPathBuilder_Dispose_Contents()
		{
			Span<char> scratch = stackalloc char[32];
			var builder = new JsonPathBuilder(scratch);

			Assert.That(builder.Length, Is.Zero);
			Assert.That(builder.Capacity, Is.EqualTo(32));
			Assert.That(builder.Span.Length, Is.Zero);
			Assert.That(builder.ToString(), Is.EqualTo(""));
			Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty));

			builder.Append(new string('A', 100));
			Assert.That(builder.Length, Is.EqualTo(100));
			Assert.That(builder.Capacity, Is.GreaterThanOrEqualTo(100));

			builder.Dispose();

			Assert.That(builder.Length, Is.Zero);
			Assert.That(builder.Capacity, Is.Zero);
		}

		[Test]
		public void Test_JsonPathBuilder_Append_Name()
		{
			using (Assert.EnterMultipleScope())
			{ // "Hello"
				using var builder = new JsonPathBuilder(32);

				builder.Append("Hello");

				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["Hello"]));
				Assert.That(builder.ToString(), Is.EqualTo("Hello"));
			}
			using (Assert.EnterMultipleScope())
			{ // "He\\o"
				using var builder = new JsonPathBuilder(32);

				builder.Append(@"He\\o");

				Assert.That(builder.ToString(), Is.EqualTo(@"He\\\\o"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[@"He\\o"]));
			}
			using (Assert.EnterMultipleScope())
			{ // "He][o"
				using var builder = new JsonPathBuilder(32);

				builder.Append("He][o");

				Assert.That(builder.ToString(), Is.EqualTo(@"He\]\[o"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["He][o"]));
			}
			using (Assert.EnterMultipleScope())
			{ // "192.168.1.23"
				using var builder = new JsonPathBuilder(32);

				builder.Append("192.168.1.23");

				Assert.That(builder.ToString(), Is.EqualTo(@"192\.168\.1\.23"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["192.168.1.23"]));
			}
		}

		[Test]
		public void Test_JsonPathBuilder_Prepend_Name()
		{
			using (Assert.EnterMultipleScope())
			{ // "Hello"
				using var builder = new JsonPathBuilder(32);

				builder.Prepend("Hello");

				Assert.That(builder.ToString(), Is.EqualTo("Hello"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["Hello"]));
			}
			using (Assert.EnterMultipleScope())
			{ // "He\\o"
				using var builder = new JsonPathBuilder(32);

				builder.Prepend(@"He\\o");

				Assert.That(builder.ToString(), Is.EqualTo(@"He\\\\o"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[@"He\\o"]));
			}
			using (Assert.EnterMultipleScope())
			{ // "He][o"
				using var builder = new JsonPathBuilder(32);

				builder.Prepend("He][o");

				Assert.That(builder.ToString(), Is.EqualTo(@"He\]\[o"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["He][o"]));
			}
			using (Assert.EnterMultipleScope())
			{ // "192.168.1.23"
				using var builder = new JsonPathBuilder(32);

				builder.Prepend("192.168.1.23");

				Assert.That(builder.ToString(), Is.EqualTo(@"192\.168\.1\.23"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["192.168.1.23"]));
			}
		}

		[Test]
		public void Test_JsonPathBuilder_Append_Index()
		{
			using (Assert.EnterMultipleScope())
			{ // 0
				using var builder = new JsonPathBuilder(32);

				builder.Append(0);

				Assert.That(builder.ToString(), Is.EqualTo("[0]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[0]));
			}
			using (Assert.EnterMultipleScope())
			{ // 1
				using var builder = new JsonPathBuilder(32);

				builder.Append(1);

				Assert.That(builder.ToString(), Is.EqualTo("[1]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[1]));
			}
			using (Assert.EnterMultipleScope())
			{ // 123
				using var builder = new JsonPathBuilder(32);

				builder.Append(123);

				Assert.That(builder.ToString(), Is.EqualTo("[123]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[123]));
			}
			using (Assert.EnterMultipleScope())
			{ // ^1
				using var builder = new JsonPathBuilder(32);

				builder.Append(^1);

				Assert.That(builder.ToString(), Is.EqualTo("[^1]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[^1]));
			}
			using (Assert.EnterMultipleScope())
			{ // ^123
				using var builder = new JsonPathBuilder(32);

				builder.Append(^123);

				Assert.That(builder.ToString(), Is.EqualTo("[^123]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[^123]));
			}
		}

		[Test]
		public void Test_JsonPathBuilder_Prepend_Index()
		{
			using (Assert.EnterMultipleScope())
			{ // 0
				using var builder = new JsonPathBuilder(32);

				builder.Prepend(0);

				Assert.That(builder.ToString(), Is.EqualTo("[0]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[0]));
			}
			using (Assert.EnterMultipleScope())
			{ // 1
				using var builder = new JsonPathBuilder(32);

				builder.Prepend(1);

				Assert.That(builder.ToString(), Is.EqualTo("[1]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[1]));
			}
			using (Assert.EnterMultipleScope())
			{ // 123
				using var builder = new JsonPathBuilder(32);

				builder.Prepend(123);

				Assert.That(builder.ToString(), Is.EqualTo("[123]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[123]));
			}
			using (Assert.EnterMultipleScope())
			{ // ^1
				using var builder = new JsonPathBuilder(32);

				builder.Prepend(^1);

				Assert.That(builder.ToString(), Is.EqualTo("[^1]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[^1]));
			}
			using (Assert.EnterMultipleScope())
			{ // ^123
				using var builder = new JsonPathBuilder(32);

				builder.Prepend(^123);

				Assert.That(builder.ToString(), Is.EqualTo("[^123]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[^123]));
			}
		}

		[Test]
		public void Test_JsonPathBuilder_Append_Name_Name()
		{
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Append("Hello");
				builder.Append("World");

				Assert.That(builder.ToString(), Is.EqualTo("Hello.World"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["Hello"]["World"]));
			}
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Append("Hosts");
				builder.Append("192.168.1.23");

				Assert.That(builder.ToString(), Is.EqualTo(@"Hosts.192\.168\.1\.23"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["Hosts"]["192.168.1.23"]));
			}
		}

		[Test]
		public void Test_JsonPathBuilder_Prepend_Name_Name()
		{
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Prepend("World");
				builder.Prepend("Hello");

				Assert.That(builder.ToString(), Is.EqualTo("Hello.World"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["Hello"]["World"]));
			}
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Prepend("192.168.1.23");
				builder.Prepend("Hosts");

				Assert.That(builder.ToString(), Is.EqualTo(@"Hosts.192\.168\.1\.23"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["Hosts"]["192.168.1.23"]));
			}
		}

		[Test]
		public void Test_JsonPathBuilder_Append_Index_Index()
		{
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Append(123);
				builder.Append(456);

				Assert.That(builder.ToString(), Is.EqualTo("[123][456]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[123][456]));
			}
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Append(^1);
				builder.Append(0);

				Assert.That(builder.ToString(), Is.EqualTo("[^1][0]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[^1][0]));
			}
		}

		[Test]
		public void Test_JsonPathBuilder_Prepend_Index_Index()
		{
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Prepend(123);
				builder.Prepend(456);

				Assert.That(builder.ToString(), Is.EqualTo("[456][123]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[456][123]));
			}
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Prepend(^1);
				builder.Prepend(0);

				Assert.That(builder.ToString(), Is.EqualTo("[0][^1]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[0][^1]));
			}
		}

		[Test]
		public void Test_JsonPathBuilder_Append_Name_Index()
		{
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Append("Hello");
				builder.Append(123);

				Assert.That(builder.ToString(), Is.EqualTo("Hello[123]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["Hello"][123]));
			}
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Append("Hell[]");
				builder.Append(^1);

				Assert.That(builder.ToString(), Is.EqualTo(@"Hell\[\][^1]"));

				var path = builder.ToPath();
				Assert.That(path, Is.EqualTo(JsonPath.Empty["Hell[]"][^1]));
				Assert.That(path.GetSegments()[0], Is.EqualTo("Hell[]"));
				Assert.That(path.GetSegments()[1], Is.EqualTo(^1));
			}
		}

		[Test]
		public void Test_JsonPathBuilder_Prepend_Name_Index()
		{
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Prepend("Hello");
				builder.Prepend(123);

				Assert.That(builder.ToString(), Is.EqualTo("[123].Hello"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[123]["Hello"]));
			}
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Prepend("Hell[]");
				builder.Prepend(^1);

				Assert.That(builder.ToString(), Is.EqualTo(@"[^1].Hell\[\]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[^1]["Hell[]"]));
			}
		}

		[Test]
		public void Test_JsonPathBuilder_Append_Index_Name()
		{
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Append(123);
				builder.Append("Hello");

				Assert.That(builder.ToString(), Is.EqualTo("[123].Hello"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[123]["Hello"]));
			}
		}

		[Test]
		public void Test_JsonPathBuilder_Prepend_Index_Name()
		{
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Prepend(123);
				builder.Prepend("Hello");

				Assert.That(builder.ToString(), Is.EqualTo("Hello[123]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["Hello"][123]));
			}
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Prepend(^1);
				builder.Prepend("Hell[]");

				Assert.That(builder.ToString(), Is.EqualTo(@"Hell\[\][^1]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["Hell[]"][^1]));
			}
		}

		[Test]
		public void Test_JsonPathBuilder_Append_Name_Index_Name()
		{
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Append("Hello");
				builder.Append(123);
				builder.Append("World");

				Assert.That(builder.ToString(), Is.EqualTo("Hello[123].World"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["Hello"][123]["World"]));
			}
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Append("Hell[]");
				builder.Append(^1);
				builder.Append(@"Wor\d");

				Assert.That(builder.ToString(), Is.EqualTo(@"Hell\[\][^1].Wor\\d"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["Hell[]"][^1][@"Wor\d"]));
			}
		}

		[Test]
		public void Test_JsonPathBuilder_Prepend_Name_Index_Name()
		{
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Prepend("World");
				builder.Prepend(123);
				builder.Prepend("Hello");

				Assert.That(builder.ToString(), Is.EqualTo("Hello[123].World"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["Hello"][123]["World"]));
			}
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Prepend(@"Wor\d");
				builder.Prepend(^1);
				builder.Prepend("Hell[]");

				Assert.That(builder.ToString(), Is.EqualTo(@"Hell\[\][^1].Wor\\d"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty["Hell[]"][^1][@"Wor\d"]));
			}
		}

		[Test]
		public void Test_JsonPathBuilder_Append_Index_Name_Name()
		{
			using (Assert.EnterMultipleScope())
			{
				using var builder = new JsonPathBuilder(32);

				builder.Append(123);
				builder.Append("Hello");
				builder.Append(456);

				Assert.That(builder.ToString(), Is.EqualTo("[123].Hello[456]"));
				Assert.That(builder.ToPath(), Is.EqualTo(JsonPath.Empty[123]["Hello"][456]));
			}
		}

	}

}
