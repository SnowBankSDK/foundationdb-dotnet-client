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
	using System.Reflection;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.HttpOverrides;
	using Microsoft.AspNetCore.Routing;
	using SnowBank.Networking.PacketCapture;

	/// <summary>Simple simulated Web Host Test that can be used to quickly scaffold a microservice or very basic network responder.</summary>
	/// <remarks>This starts very quickly and can be configured to add singleton services, custom routes, ...</remarks>
	public class MinimalWebHostTestComponent : DistributedTestComponent
	{

		internal List<Action<WebApplicationBuilder>> ConfigureServicesHandlers { get; set; } = [ ];

		internal List<Action<WebApplication>> ConfigureApplicationHandlers { get; set; } = [ ];

		internal Action<IVirtualNetworkMap> ConfigureNetworkHandler { get; set; } = (_) => { };

		internal List<Action<IEndpointRouteBuilder>> RouteHandlers { get; set; } = [ ];

		internal Func<MinimalWebHostTestComponent, CancellationToken, ValueTask> StartingHandler { get; set; } = (_, _) => default;

		internal Func<MinimalWebHostTestComponent, CancellationToken, ValueTask> StoppingHandler { get; set; } = (_, _) => default;

		internal Func<ValueTask> DisposingHandler { get; set; } = () => default;

		internal List<object> Disposables { get; } = [ ];

		public Assembly? StaticAssetsAssembly { get; set; }

		public MinimalWebHostTestComponent(string id, IVirtualNetworkLocation location, CancellationToken lifetime)
			: base(id, location, lifetime)
		{ }

		protected override void ConfigureServices(WebApplicationBuilder builder)
		{
			builder.Services.AddSingleton<VirtualNetworkProxyMiddleware>();
			builder.Services.Configure<ForwardedHeadersOptions>(options =>
			{
				options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
			});

			builder.Services.AddRouting();
			foreach (var handler in this.ConfigureServicesHandlers)
			{
				handler(builder);
			}
		}

		protected override void ConfigureApplication(WebApplication app)
		{
			app.UsePacketCapture(); //IMPORTANT: must be *AFTER* response compression, but *BEFORE* the rest!
			app.UseVirtualNetworkProxy();
			app.UseRouting();
			foreach (var handler in this.ConfigureApplicationHandlers)
			{
				handler(app);
			}

			foreach (var route in this.RouteHandlers)
			{
				route(app);
			}
		}

		protected override Assembly? GetStaticAssetsRuntimeAssembly() => this.StaticAssetsAssembly;

		protected override ValueTask OnStarting(CancellationToken ct)
		{
			var packet = this.Services.GetService<PacketCaptureManager>();
			if (packet != null && !packet.IsRunning) packet.Start();

			return this.StartingHandler(this, ct);
		}

		protected override async ValueTask OnStopping(CancellationToken ct)
		{
			var packet = this.Services.GetService<PacketCaptureManager>();
			if (packet != null && packet.IsRunning) packet.PrepareShutdown();

			foreach (var instance in this.Disposables)
			{
				ct.ThrowIfCancellationRequested();
				try
				{
					if (instance is IAsyncDisposable asyncDisposable)
					{
						await asyncDisposable.DisposeAsync();
					}
					else if (instance is IDisposable disposable)
					{
						disposable.Dispose();
					}
				}
				catch (Exception ex)
				{
					Log($"Failed to dispose registered {instance.GetType().GetFriendlyName()} instance: [{ex.GetType().Name}] {ex.Message}");
				}
			}

			await this.StoppingHandler(this, ct);
		}

		protected override ValueTask OnDisposing()
		{
			// a component whose server never started (Init/Start threw, e.g. a test that deliberately fails host
			// registration) reaches teardown with no DI container: skip the service lookup instead of letting
			// Services throw and print a failed-dispose stack trace for a component that was never up.
			if (this.HasServices)
			{
				var packet = this.Services.GetService<PacketCaptureManager>();
				packet?.Shutdown();
			}

			return this.DisposingHandler();
		}

		protected override void RegisterWithNetwork(IVirtualNetworkMap map)
		{
			map.Host.Bind(this.Location, 443, this.CreateHttpHandler);
			var loopback = this.NetworkMap.Host.Loopback;
			if (loopback != null)
			{
				map.Host.Bind(loopback, 443, this.CreateHttpHandler);
			}

			this.ConfigureNetworkHandler(map);
		}

	}

	/// <summary>Builder for a <see cref="MinimalWebHostTestComponent"/></summary>
	public class MinimalWebHostTestBuilder : IHostTestBuilder, IMinimalApiTestComponentBuilder
	{

		/// <inheritdoc/>
		public string Id => this.Component.Id;

		public required MinimalWebHostTestComponent Component { get; init; }

		/// <inheritdoc/>
		IDistributedTestComponent IHostTestBuilder.Component => this.Component;

		/// <inheritdoc/>
		public required IDistributedTestNetworkBuilder Parent { get; init; }

		/// <inheritdoc/>
		public IDistributedTestContext Context => this.Component.Context;

		public IVirtualNetworkLocation Location => this.Component.Location;

		/// <inheritdoc/>
		public VirtualHostIdentity Identity => this.Component.NetworkIdentity;

		internal List<Action<WebApplicationBuilder>> ServiceHandlers { get; } = [ ];

		internal List<Action<WebApplication>> ApplicationHandlers { get; } = [ ];

		internal Func<MinimalWebHostTestComponent, CancellationToken, ValueTask>? StartingHandler { get; private set; }

		internal Func<MinimalWebHostTestComponent, CancellationToken, ValueTask>? StoppingHandler { get; private set; }

		internal List<Action<IEndpointRouteBuilder>> RouteHandlers { get; } = [ ];

		internal List<object> Disposables { get; } = [ ];

		/// <summary>Registers a callback that will be able to add custom services to this host</summary>
		/// <remarks>This method can be called multiple times to register multiple callbacks. They will execute in the same order as they were registered.</remarks>
		public void ConfigureServices(Action<WebApplicationBuilder> handler) => this.ServiceHandlers.Add(handler);

		/// <summary>Registers a callback that will be able to configure the simulated application host</summary>
		/// <remarks>This method can be called multiple times to register multiple callbacks. They will execute in the same order as they were registered.</remarks>
		public void ConfigureApplication(Action<WebApplication> handler) => this.ApplicationHandlers.Add(handler);

		/// <summary>Adds a subcomponent to this component</summary>
		public void AddSubComponent(IDistributedTestComponent component)
		{
			this.Component.AddSubComponent(component);
		}

		/// <summary>This instance will be disposed when this host is stopped</summary>
		public void Using(object instance)
		{
			if (instance is not IDisposable or IAsyncDisposable)
			{
				throw new ArgumentException("Instance must either implement IDisposable or IAsyncDisposable");
			}

			this.Disposables.Add(instance);
		}

		/// <summary>Registers a callback that will run when the host becomes ready, but before the test method can use it.</summary>
		/// <remarks>Please note that each simulated host can start in a non-deterministic order, and this method cannot rely on other hosts to be ready as well.</remarks>
		public void OnStartup(Action handler) => this.StartingHandler = (host, ct) =>
		{
			if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
			handler();
			return default;
		};

		/// <summary>Registers a callback that will run when the host becomes ready, but before the test method can use it.</summary>
		/// <remarks>Please note that each simulated host can start in a non-deterministic order, and this method cannot rely on other hosts to be ready as well.</remarks>
		public void OnStartup(Action<IServiceProvider> handler) => this.StartingHandler = (host, ct) =>
		{
			if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
			handler(host.Services);
			return default;
		};

		/// <summary>Registers a callback that will run when the host becomes ready, but before the test method can use it.</summary>
		/// <remarks>Please note that each simulated host can start in a non-deterministic order, and this method cannot rely on other hosts to be ready as well.</remarks>
		public void OnStartup(Func<MinimalWebHostTestComponent, CancellationToken, ValueTask> handler) => this.StartingHandler = handler;

		/// <summary>Registers a callback that will run when the host becomes ready, but before the test method can use it.</summary>
		/// <remarks>Please note that all simulated hosts will start in a non-deterministic order, so this callback cannot rely on other hosts to be ready as well.</remarks>
		public void OnStartup(Delegate handler)
		{
			var magic = MagicDelegate.Create(handler);
			this.StartingHandler = (host, ct) =>
			{
				if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
				magic.Invoke(host.Services);
				return default;
			};
		}

		/// <summary>Registers a callback that will run when the test has completed (successfully or not), to help release any resources used by this host.</summary>
		/// <remarks>Please note that all simulated hosts will stop in a non-deterministic order, so this callback cannot on other hosts still being alive.</remarks>
		public void OnShutdown(Action handler) => this.StoppingHandler = (host, ct) =>
		{
			if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
			handler();
			return default;
		};

		/// <summary>Registers a callback that will run when the test has completed (successfully or not), to help release any resources used by this host.</summary>
		/// <remarks>Please note that all simulated hosts will stop in a non-deterministic order, so this callback cannot on other hosts still being alive.</remarks>
		public void OnShutdown(Action<MinimalWebHostTestComponent> handler) => this.StoppingHandler = (host, ct) =>
		{
			if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
			handler(host);
			return default;
		};

		/// <summary>Registers a callback that will run when the test has completed (successfully or not), to help release any resources used by this host.</summary>
		/// <remarks>Please note that all simulated hosts will stop in a non-deterministic order, so this callback cannot on other hosts still being alive.</remarks>
		public void OnShutdown(Func<MinimalWebHostTestComponent, CancellationToken, ValueTask> handler) => this.StoppingHandler = handler;

		/// <summary>Registers a callback that will run when the test has completed (successfully or not), to help release any resources used by this host.</summary>
		/// <remarks>Please note that all simulated hosts will stop in a non-deterministic order, so this callback cannot on other hosts still being alive.</remarks>
		public void OnShutdown(Delegate handler)
		{
			var magic = MagicDelegate.Create(handler);
			this.StoppingHandler = (host, ct) =>
			{
				if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
				magic.Invoke(host.Services);
				return default;
			};
		}

		/// <inheritdoc/>
		public void AddRoute(Action<IEndpointRouteBuilder> handler)
		{
			Contract.NotNull(handler);
			this.RouteHandlers.Add(handler);
		}

	}

}
