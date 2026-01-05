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

// ReSharper disable InconsistentNaming

namespace SnowBank.Networking.Tests
{
	using System.Net;
	using System.Net.Sockets;
	using SnowBank.Networking;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class IPAddressHelpersFacts : SimpleTest
	{

		[Test]
		public void Test_IsValidIP_V4()
		{
			static void ShouldPassV4(string ip)
			{
				Assert.That(IPAddressHelpers.IsValidIP(ip), Is.True, $"'{ip}' is a valid IP address");
				Assert.That(IPAddressHelpers.IsValidIPv4(ip), Is.True, $"'{ip}' is a valid IPv4 address");
				Assert.That(IPAddressHelpers.IsValidIPv6(ip), Is.False, $"'{ip}' is NOT an IPv4 address");
			}

			static void ShouldFailV4(string? ip)
			{
				Assert.That(IPAddressHelpers.IsValidIPv4(ip), Is.False, $"'{ip ?? "<null>"}' is NOT a valid IPv4 address");
				if (IPAddress.TryParse(ip, out var x) && x.AddressFamily == AddressFamily.InterNetworkV6)
				{
					Assert.That(IPAddressHelpers.IsValidIP(ip), Is.True, $"'{ip}' is valid but no an IPv4 address");
				}
				else
				{
					Assert.That(IPAddressHelpers.IsValidIP(ip), Is.False, $"'{ip ?? "<null>"}' is NOT a valid IP address");
				}
			}

			ShouldPassV4("127.0.0.1");
			ShouldPassV4("192.168.1.0");
			ShouldPassV4("192.168.1.23");
			ShouldPassV4("192.168.1.123");
			ShouldPassV4("192.168.1.255");
			ShouldPassV4("8.8.4.4");
			ShouldPassV4("0.0.0.0");
			ShouldPassV4("255.255.255.255");

			ShouldFailV4(null);
			ShouldFailV4("");
			ShouldFailV4("192.168.1.256");
			ShouldFailV4("192.168.256.1");
			ShouldFailV4("192.256.1.2");
			ShouldFailV4("256.1.2.3");
			ShouldFailV4("1.2.3.4.5");
			ShouldFailV4(".1.2.3.4");
			ShouldFailV4("1..2.3");
			ShouldFailV4("8.8.4.4 ");
			ShouldFailV4(" 8.8.4.4");
			ShouldFailV4("8.8 .4.4 ");

			//IPv6 should fail
			ShouldFailV4("::1");
			ShouldFailV4("fe80::40d0:a961:a90a:4fc1");
			ShouldFailV4("2001:db8::ff00:42:8329");
		}

		[Test]
		public void Test_IsValidIP_V6()
		{
			static void ShouldPassV6(string ip)
			{
				Assert.That(IPAddressHelpers.IsValidIP(ip), Is.True, $"'{ip}' is a valid IP address");
				Assert.That(IPAddressHelpers.IsValidIPv6(ip), Is.True, $"'{ip}' is a valid IPv6 address");
				Assert.That(IPAddressHelpers.IsValidIPv4(ip), Is.False, $"'{ip}' is NOT an IPv6 address");
			}

			static void ShouldFailV6(string? ip)
			{
				Assert.That(IPAddressHelpers.IsValidIPv6(ip), Is.False, $"'{ip ?? "<null>"}' is NOT a valid IPv6 address");
				if (IPAddress.TryParse(ip, out var x) && x.AddressFamily == AddressFamily.InterNetwork)
				{
					Assert.That(IPAddressHelpers.IsValidIP(ip), Is.True, $"'{ip}' is valid but no an IPv6 address");
				}
				else
				{
					Assert.That(IPAddressHelpers.IsValidIP(ip), Is.False, $"'{ip ?? "<null>"}' is NOT a valid IP address");
				}
			}

			ShouldPassV6("::1");
			ShouldPassV6("fe80::40d0:a961:a90a:4fc1");
			ShouldPassV6("fe80::40d0:a961:a90a:4fc1%100");
			ShouldPassV6("2001:0db8:0000:0000:0000:ff00:0042:8329");
			ShouldPassV6("2001:db8:0:0:0:ff00:42:8329");
			ShouldPassV6("2001:db8::ff00:42:8329");

			ShouldFailV6(null);
			ShouldFailV6("");
			ShouldFailV6(" fe80::40d0:a961:a90a:4fc1");
			ShouldFailV6("fe80::40d0:a961:a90a:4fc1 ");
			ShouldFailV6("fe80::40d0::a961:a90a:4fc1");
			ShouldFailV6(" fe80:40d0:a961:a90a:4fc1");

			//IPv4 sould fail
			ShouldFailV6("127.0.0.1");
			ShouldFailV6("192.168.1.123");
			ShouldFailV6("0.0.0.0");
			ShouldFailV6("255.255.255.255");

		}

		[Test]
		public void Test_IsPrivateNetwork()
		{
			Assert.That(() => IPAddressHelpers.IsPrivateRange(null!), Throws.ArgumentNullException.With.Property("ParamName").EqualTo("address"));

			static void ShouldPass(string ip)
			{
				Assert.That(IPAddressHelpers.IsPrivateRange(IPAddress.Parse(ip)), Is.True, $"'{ip}' is an adress in the Private Network range");
			}

			static void ShouldFail(string ip)
			{
				Assert.That(IPAddressHelpers.IsPrivateRange(IPAddress.Parse(ip)), Is.False, $"'{ip}' is NOT an adress in the Private Network range");
			}

			// 192.168/16
			ShouldFail("192.167.255.255");
			ShouldPass("192.168.0.0");
			ShouldPass("192.168.1.23");
			ShouldPass("192.168.12.34");
			ShouldPass("192.168.255.255");
			ShouldFail("192.169.0.0");
			ShouldFail("1.2.168.192");

			// 10/8
			ShouldFail("9.255.255.255");
			ShouldPass("10.0.0.0");
			ShouldPass("10.1.2.3");
			ShouldPass("10.255.255.255");
			ShouldFail("11.0.0.0");
			ShouldFail("1.2.3.10");

			// 172.16/20
			ShouldFail("172.15.255.255");
			ShouldPass("172.16.0.0");
			ShouldPass("172.23.45.67");
			ShouldPass("172.31.255.255");
			ShouldFail("172.32.0.0");
			ShouldFail("1.2.16.172");

			// Not Private
			ShouldFail("0.0.0.0");
			ShouldFail("255.255.255.255");
			ShouldFail("127.0.0.1");
			ShouldFail("127.0.0.123");
			ShouldFail("8.8.8.8");
		}

	}

}
