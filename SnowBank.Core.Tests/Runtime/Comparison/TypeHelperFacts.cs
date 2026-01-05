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

// ReSharper disable UnusedTypeParameter
// ReSharper disable MemberHidesStaticFromOuterClass
namespace SnowBank.Runtime.Tests
{
	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class TypeHelperFacts
	{

		[Test]
		public void Test_GetFriendlyName()
		{
			// native types
			Assert.That(typeof(string).GetFriendlyName(), Is.EqualTo("string"));
			Assert.That(typeof(int).GetFriendlyName(), Is.EqualTo("int"));
			Assert.That(typeof(bool).GetFriendlyName(), Is.EqualTo("bool"));

			// Simple types
			Assert.That(typeof(DateTime).GetFriendlyName(), Is.EqualTo("DateTime"));
			Assert.That(typeof(DateTimeKind).GetFriendlyName(), Is.EqualTo("DateTimeKind"));
			Assert.That(typeof(System.IO.FileStream).GetFriendlyName(), Is.EqualTo("FileStream"));

			// Generics...
			Assert.That(typeof(List<string>).GetFriendlyName(), Is.EqualTo("List<string>"));
			Assert.That(typeof(Dictionary<Guid, List<string>>).GetFriendlyName(), Is.EqualTo("Dictionary<Guid, List<string>>"));

			// Nested types
			Assert.That(typeof(Outer.Inner).GetFriendlyName(), Is.EqualTo("Outer.Inner"));
			Assert.That(typeof(Outer<string, bool>.Inner).GetFriendlyName(), Is.EqualTo("Outer<string, bool>.Inner"));
			Assert.That(typeof(Outer<string, bool>.Inner<Guid>).GetFriendlyName(), Is.EqualTo("Outer<string, bool>.Inner<Guid>"));
			Assert.That(typeof(Outer<string, bool>.SomeEnum).GetFriendlyName(), Is.EqualTo("Outer<string, bool>.SomeEnum"));
			Assert.That(typeof(Outer<string, bool>.Middle<Guid>.Inner<DateTime>).GetFriendlyName(), Is.EqualTo("Outer<string, bool>.Middle<Guid>.Inner<DateTime>"));

			// True Last Boss
			Assert.That(typeof(Dictionary<Outer<string, bool>.Inner, List<Outer<string, bool>.Middle<Guid>.Inner<DateTime>>>).GetFriendlyName(), Is.EqualTo("Dictionary<Outer<string, bool>.Inner, List<Outer<string, bool>.Middle<Guid>.Inner<DateTime>>>"));
		}

	}

	public class Outer { public class Inner { }}

	public class Outer<T1, T2>
	{
		public class Inner { } 
		public class Inner<T3> { }

		public class Middle<T3>
		{
			public class Inner<T4> { }
		}

		public enum SomeEnum { }

	}


}
