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
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.DependencyInjection;
	using NUnit.Framework;
	using SnowBank.Networking.Http;

	/// <summary>Tests the configuration override layer registered by <see cref="BetterHttpClientExtensions.AddBetterHttpClientConfiguration"/>:
	/// a section can override the operation-safe subset of a client's options (<c>Timeout</c>, <c>AllowAutoRedirect</c>,
	/// <c>AutomaticDecompression</c>, <c>Tls:Mode</c>) on top of the code-configured layers, and <c>"inherit"</c> cancels an
	/// override back down to the code-global baseline. Every test resolves the effective options with
	/// <see cref="BetterHttpClientExtensions.ResolveClientOptions"/>, which is a pure function over the registered
	/// <c>IServiceCollection</c> and needs neither an <c>INetworkMap</c> nor a built handler chain.</summary>
	[TestFixture]
	public class BetterHttpConfigurationFacts : SimpleTest
	{

		private static IConfiguration BuildConfiguration(params (string Key, string Value)[] entries)
		{
			var builder = new ConfigurationBuilder();
			builder.AddInMemoryCollection(entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)));
			return builder.Build();
		}

		[Test]
		public void Test_Absent_Section_Leaves_Code_Configuration_Unchanged()
		{
			var services = new ServiceCollection();
			services.AddBetterHttpClientDefaults();
			services.AddBetterHttpClient("foo", o => o.Timeout = TimeSpan.FromSeconds(9));
			services.AddBetterHttpClientConfiguration(BuildConfiguration());
			using var provider = services.BuildServiceProvider();

			var options = BetterHttpClientExtensions.ResolveClientOptions(provider, "foo");

			Assert.That(options.Timeout, Is.EqualTo(TimeSpan.FromSeconds(9)),
				"an absent (or empty) override section must be a no-op: the code-configured per-name Timeout stands");
		}

		[Test]
		public void Test_Defaults_Section_Overrides_The_Code_Global_Baseline_For_Every_Name()
		{
			var services = new ServiceCollection();
			services.AddBetterHttpClientDefaults(o => o.Timeout = TimeSpan.FromSeconds(5));
			services.AddBetterHttpClientConfiguration(BuildConfiguration(("BetterHttp:Defaults:Timeout", "00:00:10")));
			using var provider = services.BuildServiceProvider();

			var options = BetterHttpClientExtensions.ResolveClientOptions(provider, "anything");

			Assert.That(options.Timeout, Is.EqualTo(TimeSpan.FromSeconds(10)),
				"the configuration Defaults section must override the code-global baseline, for a name with no per-name policy");
		}

		[Test]
		public void Test_Per_Name_Override_Wins_Over_Defaults_And_Code_Per_Name()
		{
			var services = new ServiceCollection();
			services.AddBetterHttpClientDefaults(o => o.Timeout = TimeSpan.FromSeconds(5));
			services.AddBetterHttpClient("foo", o => o.Timeout = TimeSpan.FromSeconds(9));
			services.AddBetterHttpClientConfiguration(BuildConfiguration(
				("BetterHttp:Defaults:Timeout", "00:00:10"),
				("BetterHttp:Clients:foo:Timeout", "00:00:20")));
			using var provider = services.BuildServiceProvider();

			var options = BetterHttpClientExtensions.ResolveClientOptions(provider, "foo");

			Assert.That(options.Timeout, Is.EqualTo(TimeSpan.FromSeconds(20)),
				"a Clients:<name> override must win over both the configuration Defaults and the code per-name configure");
		}

		[Test]
		public void Test_Inherit_Cancels_The_Code_Per_Name_Override_Back_To_The_Code_Global_Baseline()
		{
			var services = new ServiceCollection();
			services.AddBetterHttpClientDefaults(o => o.Timeout = TimeSpan.FromSeconds(5));
			services.AddBetterHttpClient("foo", o => o.Timeout = TimeSpan.FromSeconds(99));
			services.AddBetterHttpClientConfiguration(BuildConfiguration(("BetterHttp:Clients:foo:Timeout", "inherit")));
			using var provider = services.BuildServiceProvider();

			var fooOptions = BetterHttpClientExtensions.ResolveClientOptions(provider, "foo");
			Assert.That(fooOptions.Timeout, Is.EqualTo(TimeSpan.FromSeconds(5)),
				"'inherit' on a Clients:<name> knob cancels the client's own code configure, falling back to the code-global baseline");

			var barOptions = BetterHttpClientExtensions.ResolveClientOptions(provider, "bar");
			Assert.That(barOptions.Timeout, Is.EqualTo(TimeSpan.FromSeconds(5)),
				"a name with no per-name code configure and no override still resolves to the code-global baseline");
		}

		[Test]
		public void Test_Tls_Mode_AcceptAny_Sets_A_Validation_Callback()
		{
			var services = new ServiceCollection();
			services.AddBetterHttpClientDefaults();
			services.AddBetterHttpClientConfiguration(BuildConfiguration(("BetterHttp:Defaults:Tls:Mode", "AcceptAny")));
			using var provider = services.BuildServiceProvider();

			var options = BetterHttpClientExtensions.ResolveClientOptions(provider, "foo");

			Assert.That(options.ServerCertificateCustomValidationCallback, Is.Not.Null,
				"Tls:Mode 'AcceptAny' from configuration must install a certificate validation callback");
		}

		[Test]
		public void Test_Tls_Mode_System_Clears_A_Code_Configured_Callback()
		{
			var services = new ServiceCollection();
			services.AddBetterHttpClientDefaults(o => o.AcceptSelfSignedServerCertificates());
			services.AddBetterHttpClientConfiguration(BuildConfiguration(("BetterHttp:Defaults:Tls:Mode", "System")));
			using var provider = services.BuildServiceProvider();

			var options = BetterHttpClientExtensions.ResolveClientOptions(provider, "foo");

			Assert.That(options.ServerCertificateCustomValidationCallback, Is.Null,
				"Tls:Mode 'System' from configuration must force the callback back to null, even when code accepted self-signed certificates");
		}

		[Test]
		public void Test_Tls_Mode_Unknown_Value_Throws()
		{
			var services = new ServiceCollection();
			services.AddBetterHttpClientDefaults();
			services.AddBetterHttpClientConfiguration(BuildConfiguration(("BetterHttp:Defaults:Tls:Mode", "Bogus")));
			using var provider = services.BuildServiceProvider();

			Assert.That(() => BetterHttpClientExtensions.ResolveClientOptions(provider, "foo"), Throws.InvalidOperationException,
				"an unrecognized Tls:Mode value must fail loudly instead of silently picking a default");
		}

		[Test]
		public void Test_Tls_Mode_TrustRoots_Throws_NotSupported()
		{
			var services = new ServiceCollection();
			services.AddBetterHttpClientDefaults();
			services.AddBetterHttpClientConfiguration(BuildConfiguration(("BetterHttp:Defaults:Tls:Mode", "TrustRoots")));
			using var provider = services.BuildServiceProvider();

			Assert.That(() => BetterHttpClientExtensions.ResolveClientOptions(provider, "foo"), Throws.TypeOf<NotSupportedException>(),
				"Tls:Mode 'TrustRoots' is not bindable from configuration yet: it must say so instead of silently doing nothing");
		}

		[Test]
		public void Test_Two_Registered_Sections_Compose_In_Registration_Order()
		{
			var services = new ServiceCollection();
			services.AddBetterHttpClientDefaults(o => o.Timeout = TimeSpan.FromSeconds(5));
			services.AddBetterHttpClientConfiguration(BuildConfiguration(("Section1:Defaults:Timeout", "00:00:10")), "Section1");
			services.AddBetterHttpClientConfiguration(BuildConfiguration(("Section2:Defaults:Timeout", "00:00:15")), "Section2");
			using var provider = services.BuildServiceProvider();

			var options = BetterHttpClientExtensions.ResolveClientOptions(provider, "foo");

			Assert.That(options.Timeout, Is.EqualTo(TimeSpan.FromSeconds(15)),
				"two registered sections must apply in registration order, so the later one wins when both set the same knob");
		}

	}

}
