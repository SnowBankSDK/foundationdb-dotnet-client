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

namespace SnowBank.Testing.Framework.Tests
{
	using System.Net;
	using System.Net.Http;
	using NUnit.Framework;
	using SnowBank.Networking.Http;

	/// <summary>Pins the per-client cookie and proxy switches on <see cref="BetterHttpClientOptions"/>: a client name
	/// can force cookies or the proxy OFF regardless of the transport default (defense in depth), turn them on with an
	/// explicit container or proxy, or turn cookies on with none supplied and get a container private to that name.</summary>
	[TestFixture]
	public class BetterHttpCookieProxyPolicyFacts : SimpleTest
	{

		[Test]
		public void Test_UseCookies_False_Forces_Cookies_Off()
		{
			var options = new BetterHttpClientOptions() { UseCookies = false };
			var handler = new HttpClientHandler() { UseCookies = true };

			var result = options.ConfigureTransport(handler);

			Assert.That(result, Is.SameAs(handler), "forcing cookies off must not wrap the handler");
			Assert.That(handler.UseCookies, Is.False, "cookies must be off whatever the transport default was");
		}

		[Test]
		public void Test_UseCookies_False_With_A_Container_Is_A_Configuration_Error()
		{
			var options = new BetterHttpClientOptions() { UseCookies = false, Cookies = new CookieContainer() };

			Assert.That(() => options.ConfigureTransport(new HttpClientHandler()),
				Throws.InstanceOf<InvalidOperationException>(),
				"forcing cookies off while supplying a container is contradictory and must refuse");
		}

		[Test]
		public void Test_UseCookies_True_Without_Container_Mints_One_Stable_Per_Client()
		{
			var options = new BetterHttpClientOptions() { UseCookies = true };

			var first = new HttpClientHandler();
			options.ConfigureTransport(first);
			Assert.That(first.UseCookies, Is.True);
			Assert.That(options.Cookies, Is.Not.Null, "the minted container must be stored on the options");
			Assert.That(first.CookieContainer, Is.SameAs(options.Cookies), "the chain must use the minted container");

			// a rebuild of the same client name's chain must keep the SAME cookie state
			var second = new HttpClientHandler();
			options.ConfigureTransport(second);
			Assert.That(second.CookieContainer, Is.SameAs(first.CookieContainer), "cookie state must survive a chain rebuild");

			// another client name (another options instance) must get its OWN container
			var other = new BetterHttpClientOptions() { UseCookies = true };
			var otherHandler = new HttpClientHandler();
			other.ConfigureTransport(otherHandler);
			Assert.That(otherHandler.CookieContainer, Is.Not.SameAs(first.CookieContainer), "cookie state must never cross client names");
		}

		[Test]
		public void Test_UseProxy_False_Forces_Proxy_Off()
		{
			var options = new BetterHttpClientOptions() { UseProxy = false };
			var handler = new HttpClientHandler(); // BCL default is UseProxy = true (system proxy)

			var result = options.ConfigureTransport(handler);

			Assert.That(result, Is.SameAs(handler), "forcing the proxy off must not wrap the handler");
			Assert.That(handler.UseProxy, Is.False, "the proxy must be off, system proxy included");
		}

		[Test]
		public void Test_UseProxy_False_With_A_Proxy_Is_A_Configuration_Error()
		{
			var options = new BetterHttpClientOptions() { UseProxy = false, Proxy = new WebProxy("http://127.0.0.1:8888") };

			Assert.That(() => options.ConfigureTransport(new HttpClientHandler()),
				Throws.InstanceOf<InvalidOperationException>(),
				"forcing the proxy off while supplying one is contradictory and must refuse");
		}

		[Test]
		public void Test_UseProxy_True_Without_Proxy_Means_System_Proxy()
		{
			var options = new BetterHttpClientOptions() { UseProxy = true };
			var handler = new HttpClientHandler() { UseProxy = false };

			options.ConfigureTransport(handler);

			Assert.That(handler.UseProxy, Is.True, "true with no explicit proxy means the system proxy");
			Assert.That(handler.Proxy, Is.Null, "no explicit proxy is set, the system one applies");
		}

		[Test]
		public void Test_Null_Switches_Keep_The_Historical_Behavior()
		{
			// no switches, no container, no proxy: the transport keeps its own defaults untouched
			var options = new BetterHttpClientOptions();
			var handler = new HttpClientHandler();
			var useCookiesBefore = handler.UseCookies;
			var useProxyBefore = handler.UseProxy;

			options.ConfigureTransport(handler);

			Assert.That(handler.UseCookies, Is.EqualTo(useCookiesBefore), "a null switch must not touch the transport's cookie default");
			Assert.That(handler.UseProxy, Is.EqualTo(useProxyBefore), "a null switch must not touch the transport's proxy default");
		}

	}

}
