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
	using System.Net.Http;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Http;
	using Microsoft.Extensions.DependencyInjection;
	using NUnit.Framework;
	using SnowBank.Networking;
	using SnowBank.Networking.Http;

	/// <summary>Tests for <see cref="BetterHttpClientExtensions.AddBetterHttpClientDefaults"/>: the global hook that
	/// routes every factory client (named, typed via <c>AddHttpClient&lt;TClient&gt;</c>, or a plain <c>AddHttpClient</c>)
	/// through the <see cref="INetworkMap"/> transport, so a stock client needs no per-client enrollment (and is
	/// sandboxed by construction inside a distributed test). Also covers the registration-time coordination that keeps
	/// exactly one pipeline handler and one packet-capture handler on a named policy when the defaults hook and a named client
	/// are both present.</summary>
	[TestFixture]
	public class BetterHttpClientDefaultsFacts : DistributedTest
	{

		/// <summary>Walks a built handler chain from the outermost handler down to the primary, following <see cref="DelegatingHandler.InnerHandler"/>.</summary>
		private static List<HttpMessageHandler> WalkChain(HttpMessageHandler head)
		{
			var chain = new List<HttpMessageHandler>();
			HttpMessageHandler? h = head;
			while (h is not null)
			{
				chain.Add(h);
				h = (h as DelegatingHandler)?.InnerHandler;
			}
			return chain;
		}

		[Test]
		public void Test_Defaults_Route_A_Stock_AddHttpClient_Through_The_Map()
		{
			// AddBetterHttpClientDefaults installs a ConfigureHttpClientDefaults hook whose primary handler is the map's
			// transport, applied to every factory client. So a plain AddHttpClient("weather"), with no BetterHttpClient
			// enrollment, is routed through INetworkMap - its primary handler is the map transport, not the stock socket handler.
			var services = new ServiceCollection();
			services.AddSingleton<INetworkMap, NetworkMap>();
			services.AddBetterHttpClientDefaults();
			services.AddHttpClient("weather"); // stock factory client, no BetterHttp policy of its own
			using var provider = services.BuildServiceProvider();

			var handlerFactory = provider.GetRequiredService<System.Net.Http.IHttpMessageHandlerFactory>();
			using var handler = handlerFactory.CreateHandler("weather");
			var primary = WalkChain(handler)[^1];

			Assert.That(primary, Is.InstanceOf<BetterHttpClientHandler>(),
				"a plain AddHttpClient must be routed through INetworkMap by the defaults hook (its primary is the map transport, not a stock socket handler)");
		}

		[Test]
		public void Test_Defaults_Route_A_Typed_AddHttpClient_Through_The_Map()
		{
			// same guarantee for a typed client (AddHttpClient<TClient>): the type name is the client name, and the defaults
			// hook routes it through the map too.
			var services = new ServiceCollection();
			services.AddSingleton<INetworkMap, NetworkMap>();
			services.AddBetterHttpClientDefaults();
			services.AddHttpClient<TypedProbeClient>();
			using var provider = services.BuildServiceProvider();

			var handlerFactory = provider.GetRequiredService<System.Net.Http.IHttpMessageHandlerFactory>();
			using var handler = handlerFactory.CreateHandler(nameof(TypedProbeClient));
			var primary = WalkChain(handler)[^1];

			Assert.That(primary, Is.InstanceOf<BetterHttpClientHandler>(),
				"a typed AddHttpClient<TClient> must be routed through INetworkMap by the defaults hook");
		}

		[Test]
		public void Test_Named_Client_Overrides_The_Primary_Under_The_Defaults_Hook()
		{
			// The defaults hook sets a baseline primary (map transport with the global options) for every client. A named
			// name's own ConfigurePrimaryHttpMessageHandler runs after the defaults (per-name always beats defaults in
			// IHttpClientFactory) and assigns the primary, so the name's per-name transport pipeline wins: a wrapper the
			// name adds to its transport (options.Handlers) appears in the name's chain, but not in a stock client's chain.
			var services = new ServiceCollection();
			services.AddSingleton<INetworkMap, NetworkMap>();
			services.AddBetterHttpClientDefaults();
			services.AddBetterHttpClient("catalog", options => options.Handlers.Add((inner, _, _) => new MarkerClientHandler(inner)));
			services.AddHttpClient("weather");
			using var provider = services.BuildServiceProvider();

			var handlerFactory = provider.GetRequiredService<System.Net.Http.IHttpMessageHandlerFactory>();

			using var catalog = handlerFactory.CreateHandler("catalog");
			Assert.That(WalkChain(catalog).OfType<MarkerClientHandler>().Count(), Is.EqualTo(1),
				"the named name's own transport pipeline (its options.Handlers wrapper) must build the primary, overriding the defaults' baseline primary");

			using var weather = handlerFactory.CreateHandler("weather");
			Assert.That(WalkChain(weather).OfType<MarkerClientHandler>().Count(), Is.EqualTo(0),
				"a stock client must NOT inherit the named name's per-name transport wrapper");
		}

		[Test]
		public void Test_Defaults_Plus_Named_Client_Get_Exactly_One_Pipeline_Handler_And_One_Capture()
		{
			// The compose hazard: the defaults hook adds the pipeline handler (MagicalHandler) and the capture handler to
			// every client, and the per-name setup also adds its own to each name. Two of each would run the pipeline twice and
			// record every request twice. The fix: when the defaults hook is installed it owns the shared pipeline + capture for
			// the whole factory, and the per-name setup contributes only the per-name primary + options. So every name carries exactly
			// one of each - checked here for a named client and for the default client.
			var services = new ServiceCollection();
			services.AddSingleton<INetworkMap, NetworkMap>();
			// a marker capture handler under the capture seam, exactly how the packet-capture layer registers its own
			services.AddKeyedTransient<DelegatingHandler>(BetterHttpClientExtensions.CaptureHandlerServiceKey, (_, _) => new MarkerCaptureHandler());
			services.AddBetterHttpClientDefaults();
			services.AddBetterHttpClient("catalog");
			using var provider = services.BuildServiceProvider();

			var handlerFactory = provider.GetRequiredService<System.Net.Http.IHttpMessageHandlerFactory>();

			foreach (var name in new[] { "catalog", BetterHttpClientExtensions.DefaultClientName })
			{
				using var handler = handlerFactory.CreateHandler(name);
				var chain = WalkChain(handler);
				Assert.That(chain.Count(h => h.GetType().Name == "BetterHttpPipelineHandler"), Is.EqualTo(1),
					$"'{name}' must carry exactly one pipeline handler (the defaults hook owns it; the named policy must not add a second)");
				Assert.That(chain.OfType<MarkerCaptureHandler>().Count(), Is.EqualTo(1),
					$"'{name}' must carry exactly one capture handler (a second would record every request twice)");
			}
		}

		[Test]
		public void Test_Standalone_Named_Client_Still_Gets_Its_Full_Pipeline()
		{
			// The discriminator for the compose fix: without the defaults hook, a lone named client must still get its own
			// pipeline handler and capture (nothing else provides them). This is unchanged from before the defaults hook existed.
			var services = new ServiceCollection();
			services.AddSingleton<INetworkMap, NetworkMap>();
			services.AddKeyedTransient<DelegatingHandler>(BetterHttpClientExtensions.CaptureHandlerServiceKey, (_, _) => new MarkerCaptureHandler());
			services.AddBetterHttpClient("solo");
			using var provider = services.BuildServiceProvider();

			var handlerFactory = provider.GetRequiredService<System.Net.Http.IHttpMessageHandlerFactory>();
			using var handler = handlerFactory.CreateHandler("solo");
			var chain = WalkChain(handler);

			Assert.That(chain.Count(h => h.GetType().Name == "BetterHttpPipelineHandler"), Is.EqualTo(1),
				"a standalone named client (no defaults hook) must carry its own pipeline handler");
			Assert.That(chain.OfType<MarkerCaptureHandler>().Count(), Is.EqualTo(1),
				"a standalone named client (no defaults hook) must carry its own capture handler");
		}

		[Test]
		public void Test_Repeated_Defaults_Registration_Is_Idempotent()
		{
			// AddBetterHttpClientDefaults must be safe to call more than once: a framework base registers it per host, and a
			// test or app may register it again. The shared pipeline handler + capture are installed once, never stacked - two
			// pipeline handlers would run every request's filters/credentials twice (e.g. sign a request twice, which the
			// server then rejects). Each call's own configure still composes.
			var services = new ServiceCollection();
			services.AddSingleton<INetworkMap, NetworkMap>();
			services.AddKeyedTransient<DelegatingHandler>(BetterHttpClientExtensions.CaptureHandlerServiceKey, (_, _) => new MarkerCaptureHandler());
			services.AddBetterHttpClientDefaults();
			services.AddBetterHttpClientDefaults(); // second call must not stack a second pipeline handler or capture
			using var provider = services.BuildServiceProvider();

			var handlerFactory = provider.GetRequiredService<System.Net.Http.IHttpMessageHandlerFactory>();
			using var handler = handlerFactory.CreateHandler(BetterHttpClientExtensions.DefaultClientName);
			var chain = WalkChain(handler);

			Assert.That(chain.Count(h => h.GetType().Name == "BetterHttpPipelineHandler"), Is.EqualTo(1),
				"calling the defaults hook twice must still yield exactly one pipeline handler (a second would run every request's pipeline twice)");
			Assert.That(chain.OfType<MarkerCaptureHandler>().Count(), Is.EqualTo(1),
				"calling the defaults hook twice must still yield exactly one capture handler");
		}

		[Test]
		public async Task Test_Stock_AddHttpClient_Is_Sandboxed_Into_The_Virtual_Network()
		{
			// End to end: on a simulated host, a stock AddHttpClient (no BetterHttpClient enrollment) reaches a virtual host by
			// its simulated URI. A real socket could never resolve a ".simulated" name, so this only passes if the per-host
			// defaults hook (DistributedTestComponent's default client) pulled the stock client into the virtual network map.
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong"));
					host.ConfigureServices(builder => builder.Services.AddHttpClient("probe"));
				});
			}));
			var web = context.GetWebHost("WEB");
			var uri = web.GetUri("/ping");

			using var stock = web.GetRequiredService<System.Net.Http.IHttpClientFactory>().CreateClient("probe");
			Assert.That(await stock.GetStringAsync(uri, this.Cancellation), Is.EqualTo("pong"),
				"a stock AddHttpClient must be sandboxed into the virtual network by the per-host defaults hook");
		}

		/// <summary>Typed client used to prove <c>AddHttpClient&lt;TClient&gt;</c> is routed through the map.</summary>
		public sealed class TypedProbeClient
		{
			public TypedProbeClient(HttpClient client) => this.Client = client;
			private HttpClient Client { get; }
		}

		/// <summary>Pass-through handler a named client adds to its own transport, so a test can see the name's per-name pipeline built the primary.</summary>
		private sealed class MarkerClientHandler : DelegatingHandler
		{
			public MarkerClientHandler(HttpMessageHandler inner) : base(inner) { }
		}

		/// <summary>Marker capture handler registered under <see cref="BetterHttpClientExtensions.CaptureHandlerServiceKey"/>, standing in for the packet-capture layer's outer handler.</summary>
		private sealed class MarkerCaptureHandler : DelegatingHandler
		{ }

	}

}
