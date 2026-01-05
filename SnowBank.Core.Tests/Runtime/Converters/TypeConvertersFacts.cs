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

namespace SnowBank.Runtime.Converters.Tests
{

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class TypeConvertersFacts : SimpleTest
	{

		[Test]
		public void Test_Can_Convert_Numbers_To_Bool()
		{
			Assert.That(TypeConverters.Convert<sbyte, bool>(0), Is.False);
			Assert.That(TypeConverters.Convert<byte, bool>(0), Is.False);
			Assert.That(TypeConverters.Convert<short, bool>(0), Is.False);
			Assert.That(TypeConverters.Convert<ushort, bool>(0), Is.False);
			Assert.That(TypeConverters.Convert<int, bool>(0), Is.False);
			Assert.That(TypeConverters.Convert<uint, bool>(0), Is.False);
			Assert.That(TypeConverters.Convert<long, bool>(0), Is.False);
			Assert.That(TypeConverters.Convert<ulong, bool>(0), Is.False);
			Assert.That(TypeConverters.Convert<float, bool>(0.0f), Is.False);
			Assert.That(TypeConverters.Convert<float, bool>(float.NaN), Is.False);
			Assert.That(TypeConverters.Convert<double, bool>(0.0d), Is.False);
			Assert.That(TypeConverters.Convert<double, bool>(double.NaN), Is.False);

			Assert.That(TypeConverters.Convert<sbyte, bool>(123), Is.True);
			Assert.That(TypeConverters.Convert<byte, bool>(123), Is.True);
			Assert.That(TypeConverters.Convert<short, bool>(123), Is.True);
			Assert.That(TypeConverters.Convert<ushort, bool>(123), Is.True);
			Assert.That(TypeConverters.Convert<int, bool>(123), Is.True);
			Assert.That(TypeConverters.Convert<uint, bool>(123), Is.True);
			Assert.That(TypeConverters.Convert<long, bool>(123), Is.True);
			Assert.That(TypeConverters.Convert<ulong, bool>(123), Is.True);
			Assert.That(TypeConverters.Convert<float, bool>(123.0f), Is.True);
			Assert.That(TypeConverters.Convert<double, bool>(123.0d), Is.True);
		}

		[Test]
		public void Test_Can_Convert_Numbers_To_Int32()
		{
			Assert.That(TypeConverters.Convert<sbyte, int>(123), Is.EqualTo(123));
			Assert.That(TypeConverters.Convert<byte,	int>(123), Is.EqualTo(123));
			Assert.That(TypeConverters.Convert<short, int>(123), Is.EqualTo(123));
			Assert.That(TypeConverters.Convert<ushort, int>(123), Is.EqualTo(123));
			Assert.That(TypeConverters.Convert<int, int>(123), Is.EqualTo(123));
			Assert.That(TypeConverters.Convert<uint, int>(123), Is.EqualTo(123));
			Assert.That(TypeConverters.Convert<long, int>(123), Is.EqualTo(123));
			Assert.That(TypeConverters.Convert<ulong, int>(123), Is.EqualTo(123));
			Assert.That(TypeConverters.Convert<float, int>(123.0f), Is.EqualTo(123));
			Assert.That(TypeConverters.Convert<double, int>(123.0d), Is.EqualTo(123));
		}

		[Test]
		public void Test_Can_Convert_Numbers_To_UInt32()
		{
			Assert.That(TypeConverters.Convert<sbyte, uint>(123), Is.EqualTo(123U));
			Assert.That(TypeConverters.Convert<byte, uint>(123), Is.EqualTo(123U));
			Assert.That(TypeConverters.Convert<short, uint>(123), Is.EqualTo(123U));
			Assert.That(TypeConverters.Convert<ushort, uint>(123), Is.EqualTo(123U));
			Assert.That(TypeConverters.Convert<int, uint>(123), Is.EqualTo(123U));
			Assert.That(TypeConverters.Convert<uint, uint>(123), Is.EqualTo(123U));
			Assert.That(TypeConverters.Convert<long, uint>(123), Is.EqualTo(123U));
			Assert.That(TypeConverters.Convert<ulong, uint>(123), Is.EqualTo(123U));
			Assert.That(TypeConverters.Convert<float, uint>(123.0f), Is.EqualTo(123U));
			Assert.That(TypeConverters.Convert<double, uint>(123.0d), Is.EqualTo(123U));
		}

		[Test]
		public void Test_Can_Convert_Numbers_To_Int64()
		{
			Assert.That(TypeConverters.Convert<sbyte, long>(123), Is.EqualTo(123L));
			Assert.That(TypeConverters.Convert<byte, long>(123), Is.EqualTo(123L));
			Assert.That(TypeConverters.Convert<short, long>(123), Is.EqualTo(123L));
			Assert.That(TypeConverters.Convert<ushort, long>(123), Is.EqualTo(123L));
			Assert.That(TypeConverters.Convert<int, long>(123), Is.EqualTo(123L));
			Assert.That(TypeConverters.Convert<uint, long>(123), Is.EqualTo(123L));
			Assert.That(TypeConverters.Convert<long, long>(123), Is.EqualTo(123L));
			Assert.That(TypeConverters.Convert<ulong, long>(123), Is.EqualTo(123L));
			Assert.That(TypeConverters.Convert<float, long>(123.0f), Is.EqualTo(123L));
			Assert.That(TypeConverters.Convert<double, long>(123.0d), Is.EqualTo(123L));
		}

		[Test]
		public void Test_Can_Convert_Numbers_To_UInt64()
		{
			Assert.That(TypeConverters.Convert<sbyte, ulong>(123), Is.EqualTo(123UL));
			Assert.That(TypeConverters.Convert<byte, ulong>(123), Is.EqualTo(123UL));
			Assert.That(TypeConverters.Convert<short, ulong>(123), Is.EqualTo(123UL));
			Assert.That(TypeConverters.Convert<ushort, ulong>(123), Is.EqualTo(123UL));
			Assert.That(TypeConverters.Convert<int, ulong>(123), Is.EqualTo(123UL));
			Assert.That(TypeConverters.Convert<uint, ulong>(123), Is.EqualTo(123UL));
			Assert.That(TypeConverters.Convert<long, ulong>(123), Is.EqualTo(123UL));
			Assert.That(TypeConverters.Convert<ulong, ulong>(123), Is.EqualTo(123UL));
			Assert.That(TypeConverters.Convert<float, ulong>(123.0f), Is.EqualTo(123UL));
			Assert.That(TypeConverters.Convert<double, ulong>(123.0d), Is.EqualTo(123UL));
		}

		[Test]
		public void Test_Can_Convert_Numbers_To_Single()
		{
			Assert.That(TypeConverters.Convert<sbyte, float>(123), Is.EqualTo(123f));
			Assert.That(TypeConverters.Convert<byte, float>(123), Is.EqualTo(123f));
			Assert.That(TypeConverters.Convert<short, float>(123), Is.EqualTo(123f));
			Assert.That(TypeConverters.Convert<ushort, float>(123), Is.EqualTo(123f));
			Assert.That(TypeConverters.Convert<int, float>(123), Is.EqualTo(123f));
			Assert.That(TypeConverters.Convert<uint, float>(123), Is.EqualTo(123f));
			Assert.That(TypeConverters.Convert<long, float>(123), Is.EqualTo(123f));
			Assert.That(TypeConverters.Convert<ulong, float>(123), Is.EqualTo(123f));
			Assert.That(TypeConverters.Convert<float, float>(123.0f), Is.EqualTo(123f));
			Assert.That(TypeConverters.Convert<double, float>(123.0d), Is.EqualTo(123f));
		}

		[Test]
		public void Test_Can_Convert_Numbers_To_Double()
		{
			Assert.That(TypeConverters.Convert<sbyte, double>(123), Is.EqualTo(123d));
			Assert.That(TypeConverters.Convert<byte, double>(123), Is.EqualTo(123d));
			Assert.That(TypeConverters.Convert<short, double>(123), Is.EqualTo(123d));
			Assert.That(TypeConverters.Convert<ushort, double>(123), Is.EqualTo(123d));
			Assert.That(TypeConverters.Convert<int, double>(123), Is.EqualTo(123d));
			Assert.That(TypeConverters.Convert<uint, double>(123), Is.EqualTo(123d));
			Assert.That(TypeConverters.Convert<long, double>(123), Is.EqualTo(123d));
			Assert.That(TypeConverters.Convert<ulong, double>(123), Is.EqualTo(123d));
			Assert.That(TypeConverters.Convert<float, double>(123.0f), Is.EqualTo(123d));
			Assert.That(TypeConverters.Convert<double, double>(123.0d), Is.EqualTo(123d));
		}

		[Test]
		public void Test_Can_Convert_Numbers_To_String()
		{
			Assert.That(TypeConverters.Convert<sbyte, string>(123), Is.EqualTo("123"));
			Assert.That(TypeConverters.Convert<byte, string>(123), Is.EqualTo("123"));
			Assert.That(TypeConverters.Convert<short, string>(123), Is.EqualTo("123"));
			Assert.That(TypeConverters.Convert<ushort, string>(123), Is.EqualTo("123"));
			Assert.That(TypeConverters.Convert<int, string>(123), Is.EqualTo("123"));
			Assert.That(TypeConverters.Convert<uint, string>(123), Is.EqualTo("123"));
			Assert.That(TypeConverters.Convert<long, string>(123), Is.EqualTo("123"));
			Assert.That(TypeConverters.Convert<ulong, string>(123), Is.EqualTo("123"));
			Assert.That(TypeConverters.Convert<float, string>(123.0f), Is.EqualTo("123"));
			Assert.That(TypeConverters.Convert<float, string>(123.4f), Is.EqualTo("123.4"));
			Assert.That(TypeConverters.Convert<double, string>(123.0d), Is.EqualTo("123"));
			Assert.That(TypeConverters.Convert<double, string>(123.4d), Is.EqualTo("123.4"));
		}

	}

}
