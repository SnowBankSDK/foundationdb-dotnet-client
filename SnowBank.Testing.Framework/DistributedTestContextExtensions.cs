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

namespace SnowBank.Testing.Framework
{
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Http;

	/// <summary>Helper methods for configuration a distributed test environment.</summary>
	[PublicAPI]
	public static class DistributedTestContextExtensions
	{

		#region Networks...

		/// <summary>Adds a primary Local Network to the test environment (192.168.1/24, *.lan.simulated)</summary>
		public static IVirtualNetworkLocation AddSimpleLan(this IDistributedTestEnvironmentBuilder builder, Action<IDistributedTestNetworkBuilder> configureHosts)
		{
			return builder.AddLocation(
				"LAN",
				"Local Area Network",
				VirtualNetworkType.LocalNetwork,
				configureHosts,
				(options) =>
				{
					options.AllowsIncoming = false;
					options.IpRange = "192.168.1.0/24";
					options.DnsSuffix = ".lan.simulated";
					//?
				}
			);
		}

		/// <summary>Adds a secondary Local Network to the test environment (10.0.1.1/24, *.lan2.simulated)</summary>
		public static IVirtualNetworkLocation AddAlternativeLan(this IDistributedTestEnvironmentBuilder builder, Action<IDistributedTestNetworkBuilder> configureHosts)
		{
			return builder.AddLocation(
				"LAN2",
				"Local Area Network (#2)",
				VirtualNetworkType.LocalNetwork,
				configureHosts,
				(options) =>
				{
					options.AllowsIncoming = false;
					options.IpRange = "10.0.1.0/24";
					options.DnsSuffix = ".lan2.simulated";
					//?
				}
			);
		}

		/// <summary>Adds a custom Local Network to the test environment</summary>
		public static IVirtualNetworkLocation AddLanNetwork(this IDistributedTestEnvironmentBuilder builder, string id, string name, Action<IDistributedTestNetworkBuilder> configureHosts, Action<VirtualNetworkLocationOptions>? configureNetwork = null)
		{
			return builder.AddLocation(
				id, name, VirtualNetworkType.LocalNetwork,
				configureHosts,
				(options) =>
				{
					options.AllowsIncoming = false;
					configureNetwork?.Invoke(options);
				}
			);
		}

		/// <summary>Adds a primary Cloud to the test environment</summary>
		public static IVirtualNetworkLocation AddSimpleCloud(this IDistributedTestEnvironmentBuilder builder, Action<IDistributedTestNetworkBuilder> configureHosts)
		{
			return builder.AddLocation(
				"CLOUD",
				"Cloud In The Sky",
				VirtualNetworkType.Cloud,
				configureHosts,
				(options) =>
				{
					options.AllowsIncoming = true;
					options.IpRange = "1.2.3.0/24";
					options.DnsSuffix = ".cloud.simulated";
					//?
				}
			);
		}

		/// <summary>Adds a primary Cloud to the test environment</summary>
		public static IVirtualNetworkLocation AddPublicCloud(this IDistributedTestEnvironmentBuilder builder, string id, string name, Action<IDistributedTestNetworkBuilder> configureHosts, Action<VirtualNetworkLocationOptions>? configureNetwork = null)
		{
			return builder.AddLocation(
				id, name, VirtualNetworkType.Cloud,
				configureHosts,
				(options) =>
				{
					options.AllowsIncoming = true;
					configureNetwork?.Invoke(options);
				}
			);
		}

		#endregion

		#region Generic Hosts...

		/// <summary>Adds a simulated <see cref="GenericHostTestComponent">Generic Host</see> to this virtual network</summary>
		public static GenericHostTestComponent WithGenericHost(this IDistributedTestNetworkBuilder builder, string id, Action<GenericHostTestComponent>? configure = null)
		{
			Contract.NotNull(builder);
			Contract.NotNullOrEmpty(id);

			var host = new GenericHostTestComponent(id, builder.Location, builder.Top.Lifetime);
			configure?.Invoke(host);
			builder.RegisterComponent(host);
			builder.SetNamedComponent(id, host);
			builder.Location.RegisterNetworkService("host", id, "generic");
			builder.Location.RegisterNetworkService("generic", id, null);
			return host;
		}

		/// <summary>Returns a <see cref="GenericHostTestComponent">Generic Host</see> hosted by this test environment</summary>
		public static GenericHostTestComponent GetGenericHost(this IDistributedTestFeatureCollection context, string id)
		{
			return context.GetNamedComponent<GenericHostTestComponent>(id);
		}

		/// <summary>Adds a simulated <see cref="MinimalWebHostTestComponent">Web Host</see> to this virtual network</summary>
		public static MinimalWebHostTestComponent WithMinimalWebHost(this IDistributedTestNetworkBuilder builder, string id, Action<MinimalWebHostTestBuilder> configure)
		{
			Contract.NotNull(builder);
			Contract.NotNullOrEmpty(id);
			Contract.NotNull(configure);

			var host = new MinimalWebHostTestComponent(id, builder.Location, builder.Top.Lifetime);
			var hostBuilder = new MinimalWebHostTestBuilder() { Component = host, Parent = builder };
			configure(hostBuilder);

			host.ConfigureServicesHandlers.AddRange(hostBuilder.ServiceHandlers);
			host.ConfigureApplicationHandlers.AddRange(hostBuilder.ApplicationHandlers);
			host.StartingHandler = hostBuilder.StartingHandler ?? host.StartingHandler;
			host.StoppingHandler = hostBuilder.StoppingHandler ?? host.StoppingHandler;

			host.RouteHandlers.AddRange(hostBuilder.RouteHandlers);
			host.Disposables.AddRange(hostBuilder.Disposables);

			builder.RegisterComponent(host);
			builder.SetNamedComponent(id, host);
			builder.Location.RegisterNetworkService("host", id, "minimal");
			builder.Location.RegisterNetworkService("minimal", id, null);
			return host;
		}

		/// <summary>Returns a simulated <see cref="MinimalWebHostTestComponent">Web Host</see> hosted by this test environment</summary>
		public static MinimalWebHostTestComponent GetWebHost(this IDistributedTestFeatureCollection context, string id)
		{
			return context.GetNamedComponent<MinimalWebHostTestComponent>(id);
		}

		/// <summary>Add a new simulated <see cref="WebBrowserTestComponent">Web Browser</see>, that runs on this host</summary>
		/// <param name="builder">Host that will run the virtualized web browser "process"</param>
		/// <param name="id">Id suffix of this web browser added to the id of the host (ex: if host is "CLIENT" and browser id is "CHROME", the component will have id "CLIENT:CHROME")</param>
		/// <param name="configure">Configure the browser</param>
		public static WebBrowserTestComponent WithWebBrowser(this IHostTestBuilder builder, string id, Action<WebBrowserTestComponentBuilder> configure)
		{
			Contract.NotNull(builder);
			Contract.NotNullOrEmpty(id);
			Contract.NotNull(configure);

			id = builder.Component.Id + ":" + id;

			var browser = new WebBrowserTestComponent(builder.Component, id, builder.Parent.Location, builder.Parent.Top.Lifetime);
			var hostBuilder = new WebBrowserTestComponentBuilder() { Component = browser, Builder = builder };
			configure(hostBuilder);

			builder.AddSubComponent(browser);

			browser.ConfigureServicesHandlers.AddRange(hostBuilder.ServiceHandlers);
			browser.StartingHandler = hostBuilder.StartingHandler ?? browser.StartingHandler;
			browser.StoppingHandler = hostBuilder.StoppingHandler ?? browser.StoppingHandler;
			browser.Disposables.AddRange(hostBuilder.Disposables);

			//host.Parent.RegisterComponent(browser);
			builder.Parent.SetNamedComponent(id, browser);
			//builder.Location.RegisterNetworkService("host", id, "minimal");
			//builder.Location.RegisterNetworkService("minimal", id, null);
			builder.Parent.Location.RegisterNetworkService("browser", id, null);
			return browser;
		}

		/// <summary>Returns a simulated <see cref="MinimalWebHostTestComponent">Web Host</see> hosted by this test environment</summary>
		public static WebBrowserTestComponent GetWebBrowser(this IDistributedTestFeatureCollection context, string id)
		{
			return context.GetNamedComponent<WebBrowserTestComponent>(id);
		}

		#endregion

		#region Features...

		/// <summary>Gets a feature from this test environment</summary>
		public static TFeature GetOrCreateFeature<TFeature>(this IDistributedTestFeatureCollection collection, Func<TFeature> factory)
		{
			if (!collection.TryGetFeature<TFeature>(out var feature))
			{
				feature = factory();
				collection.SetFeature(feature);
			}
			return feature;
		}

		#region Feature: Dynamic Configuration Parameters...

		// The "Dynamic Parameters" are a mini feature that allows any component to interact with other components during the test execution, in order to exchange data or settings without creating circular references in the code
		// For exemple, a Backend component can publish a callback that allows any Agent to get some URL or other dynamically computed value.
		// Each "parameter" has a unique key (ex: "foo.bar.counter"), and the callers must know the expected value type (or risk throwing with InvalidCastException)

		private static Dictionary<string, Delegate> GetDynamicParameters(this IDistributedTestFeatureCollection collection)
		{
			Contract.NotNull(collection);
			return collection.GetOrCreateFeature<Dictionary<string, Delegate>>(() => new Dictionary<string, Delegate>(StringComparer.Ordinal));
		}

		/// <summary>Adds a new Dynamic Parameter to this test environment</summary>
		public static void SetDynamicParameter<TValue>(this IDistributedTestFeatureCollection collection, string id, Func<TValue> factory)
		{
			Contract.NotNullOrEmpty(id);
			var parameters = GetDynamicParameters(collection);
			parameters[id] = factory;
		}

		/// <summary>Adds a new Dynamic Parameter to this test environment</summary>
		public static void SetDynamicParameter<TContext, TValue>(this IDistributedTestFeatureCollection collection, string id, TContext context, Func<TContext, TValue> factory)
		{
			SetDynamicParameter(collection, id, MakeFactory(context, factory));
			static Func<TValue> MakeFactory(TContext context, Func<TContext, TValue> factory) => () => factory(context);
		}

		/// <summary>Gets the value of a Dynamic Parameter in this test environment</summary>
		public static bool TryGetDynamicParameter<TValue>(this IDistributedTestFeatureCollection collection, string id, [MaybeNullWhen(false)] out TValue value)
		{
			Contract.NotNullOrEmpty(id);
			var parameters = GetDynamicParameters(collection);
			if (!parameters.TryGetValue(id, out var handler))
			{
				value = default;
				return false;
			}

			if (handler is not Func<TValue> factory)
			{
				throw new InvalidOperationException($"The dynamic parameter '{id}' has a factory of type {handler.GetType().GetFriendlyName()} instead of expected type {typeof(Func<TValue>).GetFriendlyName()}.");
			}

			value = factory();
			return true;
		}

		/// <summary>Gets the value of a Dynamic Parameter in this test environment</summary>
		public static TValue GetDynamicParameter<TValue>(this IDistributedTestFeatureCollection collection, string id)
		{
			Contract.NotNullOrEmpty(id);
			var parameters = GetDynamicParameters(collection);
			if (!parameters.TryGetValue(id, out var handler))
			{
				throw new ArgumentException($"No such dynamic parameter: '{id}'", nameof(id));
			}

			if (handler is not Func<TValue> factory)
			{
				throw new InvalidOperationException($"The dynamic parameter '{id}' has a factory of type {handler.GetType().GetFriendlyName()} instead of expected type {typeof(Func<TValue>).GetFriendlyName()}.");
			}

			return factory();
		}

		#endregion

		#region Feature: Named Components...

		// This feature allows creating named collections of components of the same Type. This is similar to the Keyed Services in the DI
		// For example, if there are multiple components of type "FooComponent", they can be managed separately using SetNamedComponent<FooComponent>(id) or TryGetNamedComponent<FooComponent>(id).
		// Each identifier is unique per component type.

		/// <summary>Registers a new Named Component in this test environment</summary>
		public static void SetNamedComponent<TComponent>(this IDistributedTestFeatureCollection collection, string id, TComponent component)
		{
			var map = collection.GetOrCreateFeature(() => new Dictionary<string, TComponent>(StringComparer.Ordinal));
			if (map.TryGetValue(id, out _))
			{
				throw new AssertionException($"There is already a {typeof(TComponent).GetFriendlyName()} component named '{id}'");
			}
			map.Add(id, component);

			// also register it to the global list if it's a test host
			if (component is IDistributedTestComponent host)
			{
				var global = collection.GetOrCreateFeature(() => new Dictionary<string, IDistributedTestComponent>(StringComparer.Ordinal));
				global.Add(host.Id, host);
			}
		}

		/// <summary>Gets the Named Component with the given identifier</summary>
		public static bool TryGetNamedComponent<TComponent>(this IDistributedTestFeatureCollection collection, string id, [MaybeNullWhen(false)] out TComponent component)
		{
			component = default;
			return collection.TryGetFeature<Dictionary<string, TComponent>>(out var items) && items.TryGetValue(id, out component);
		}

		/// <summary>Gets the Named Component with the given identifier</summary>
		public static TComponent GetNamedComponent<TComponent>(this IDistributedTestFeatureCollection collection, string id)
		{
			return collection.TryGetFeature<Dictionary<string, TComponent>>(out var items) && items.TryGetValue(id, out var component)
				? component
				: throw new AssertionException($"There is no {typeof(TComponent).GetFriendlyName()} component named '{id}'");
		}

		/// <summary>Gets the Virtual Host with the given identifier</summary>
		public static IDistributedTestComponent GetHost(this IDistributedTestFeatureCollection collection, string id)
		{
			return collection.TryGetFeature<Dictionary<string, IDistributedTestComponent>>(out var items) && items.TryGetValue(id, out var component)
				? component
				: throw new AssertionException($"There is no virtual test host named '{id}'");
		}

		#endregion

		#region Feature: Test Knobs...

		// "Test Knobs" are a mini-feature that is used by some components to set or change configuration settings, temp value or callback, during the test setup or execution.
		// For exemple, a unique entity counter that can be used to generate unique IP addresses
		// Each "knob" has a unique key (ex: "foo.bar.counter"), and the caller must know the expected value type

		private static Dictionary<string, TestKnob> GetKnobs(this IDistributedTestFeatureCollection collection)
		{
			return collection.GetOrCreateFeature<Dictionary<string, TestKnob>>(() => new Dictionary<string, TestKnob>(StringComparer.Ordinal));
		}

		/// <summary>Sets the value of a Test Knob</summary>
		public static TValue? SetKnob<TValue>(this IDistributedTestFeatureCollection collection, string key, TValue value)
		{
			var knobs = collection.GetKnobs();
			if (!knobs.TryGetValue(key, out var knob))
			{
				knob = new TestKnob(key, value);
				knobs[key] = knob;
				return default;
			}
			else
			{
				var previous = knob.Get<TValue>();
				knob.Value = value;
				return previous;
			}
		}

		/// <summary>Gets the value of the Test Knob with the given identifier</summary>
		public static bool TryGetKnob<TValue>(this IDistributedTestFeatureCollection collection, string key, [MaybeNullWhen(false)] out TValue value)
		{
			var knobs = collection.GetKnobs();
			if (!knobs.TryGetValue(key, out var knob))
			{
				value = default;
				return false;
			}

			value = knob.Get<TValue>();
			return true;
		}

		/// <summary>Gets the value of the Test Knob with the given identifier</summary>
		public static TValue GetOrCreateKnob<TValue>(this IDistributedTestFeatureCollection collection, string key, Func<TValue> factory)
		{
			var knobs = collection.GetKnobs();
			if (knobs.TryGetValue(key, out var knob))
			{
				return knob.Get<TValue>();
			}

			var value = factory();
			knob = new TestKnob(key, factory());
			knobs[key] = knob;
			return value;
		}

		/// <summary>Changes the value of a Test Knob</summary>
		public static TValue MutateKnob<TValue>(this IDistributedTestFeatureCollection collection, string key, Func<TValue> factory, Func<TValue, (TValue ReturnValue, TValue UpdatedValue)> mutate)
		{
			var knobs = collection.GetKnobs();

			if (!knobs.TryGetValue(key, out var knob))
			{
				var (returnValue, updatedValue) = mutate(factory());
				knob = new TestKnob(key, updatedValue); // temporary!
				knobs[key] = knob;
				return returnValue;
			}
			else
			{
				var (returnValue, updatedValue) = mutate(knob.Get<TValue>());
				knob.Value = updatedValue;
				return returnValue;
			}
		}

		#endregion

		#endregion

		#region Test Helpers...

		/// <summary>Adds several test routes to this host (<c>/test/status</c>, <c>/test/echo</c>, <c>/test/log</c>)</summary>
		public static void UseTestResponder(this IApplicationBuilder app, string component)
		{
			app.UseEndpoints(builder =>
			{
				#region Test Endpoints...
				// Endpoints spécifiques pour les tests unitaires

				// GET /test/status => 200 OK, { "Code": "success", "SysTime": "....", "Rnd": "GUID" }
				builder.MapGet(
					"/test/status",
					async context =>
					{
						//HACKHACK: je sais pas vraiment comment faire ça de manière idiomatique!
						if (context.Request.Headers["Accept"].Any(x => x == "application/json" || (x is not null && x.StartsWith("application/json;", StringComparison.Ordinal))))
						{
							var json = new JsonObject
							{
								["Code"] = "success",
								["SysTime"] = SystemClock.Instance.GetCurrentInstant(),
								["Rnd"] = Guid.NewGuid(),
								["Component"] = component,
							};
							await context.Response.WriteAsync(json.ToJsonTextIndented(), Encoding.UTF8);
							return;
						}

						await context.Response.WriteAsync("success");
					});

				// POST /test/push + JSON => 200 OK
				builder.MapPost(
					"/test/echo",
					async context =>
					{
						var contentType = context.Request.GetTypedHeaders().ContentType;
						switch (contentType?.MediaType.Value)
						{
							case "application/json":
							{
								var body = CrystalJson.Parse(await new StreamReader(context.Request.Body, Encoding.UTF8).ReadToEndAsync());
								var json = new JsonObject { ["Code"] = "success", ["Echo"] = body };
								await context.Response.WriteAsync(json.ToJsonTextIndented());
								break;
							}
							default:
							{
								var content = await new StreamReader(context.Request.Body, Encoding.UTF8).ReadToEndAsync();
								await context.Response.WriteAsync(content, Encoding.UTF8);
								break;
							}
						}
					});

				builder.MapPut("/test/log", _ => Task.CompletedTask);
				#endregion
			});
		}

		#endregion

	}

}
