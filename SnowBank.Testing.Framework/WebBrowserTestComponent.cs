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
	using System.Net;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Hosting;
	using Microsoft.AspNetCore.TestHost;
	using Microsoft.Extensions.Configuration;
	using SnowBank.Networking.PacketCapture;

	/// <summary>Simple simulated Web Browser, that runs as a subcomponent of a simulated host.</summary>
	[PublicAPI]
	public class WebBrowserTestComponent : DistributedTestComponent
	{

		public WebBrowserTestComponent(IDistributedTestComponent parent, string id, IVirtualNetworkLocation location, CancellationToken lifetime)
			: base(id, location, lifetime, parent)
		{
			//TODO: user agent?
		}

		internal List<Action<IServiceCollection>> ConfigureServicesHandlers { get; set; } = [ ];

		internal Func<WebBrowserTestComponent, CancellationToken, ValueTask> StartingHandler { get; set; } = (_, _) => default;

		internal Func<WebBrowserTestComponent, CancellationToken, ValueTask> StoppingHandler { get; set; } = (_, _) => default;

		internal Func<ValueTask> DisposingHandler { get; set; } = () => default;

		internal List<object> Disposables { get; } = [ ];

		protected override void OnRegisterComponent(IDistributedTestContext context)
		{
			//
		}

		protected override void ConfigureServices(WebApplicationBuilder builder)
		{
			foreach (var handler in this.ConfigureServicesHandlers)
			{
				handler(builder.Services);
			}
		}

		protected override void ConfigureApplication(WebApplication app)
		{
			//
			app.UsePacketCapture(); //IMPORTANT: must be *AFTER* response compression, but *BEFORE* the rest!
		}

		protected override ValueTask OnInitialize(TestServer server, IConfiguration config, CancellationToken startToken)
		{
			return default;
		}

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
					if (instance is IAsyncDisposable asyncDisp)
					{
						await asyncDisp.DisposeAsync();
					}
					else if (instance is IDisposable disp)
					{
						disp.Dispose();
					}
				}
				catch (Exception ex)
				{
					Log($"Failed to dispose registerd {instance.GetType().GetFriendlyName()} instance: [{ex.GetType().Name}] {ex.Message}");
				}
			}

			await this.StoppingHandler(this, ct);
		}

		protected override ValueTask OnDisposing()
		{
			var packet = this.Services.GetService<PacketCaptureManager>();
			if (packet != null) packet.Shutdown();

			return this.DisposingHandler();
		}

		public sealed record PageResult
		{
			public required Uri Location { get; init; }

			public required HttpStatusCode Status { get; init; }

			public required byte[]? Body { get; init; }

			/// <summary>Returns the "about:blank" page</summary>
			public static PageResult AboutBlank() => new() { Location = new Uri("about:blank"), Status = HttpStatusCode.OK, Body = [] };

			public string? ReadAsText() => this.Body.AsSlice().ToStringUtf8();

		}

		public PageResult Page { get; private set; } = PageResult.AboutBlank();

		public Task<PageResult> NavigateTo(string uri, CancellationToken ct) => NavigateTo(new Uri(uri), ct);

		/// <summary>Navigate to the specified URI, with a GET request</summary>
		public async Task<PageResult> NavigateTo(Uri uri, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			var cli = this.GetBetterHttpClient(uri);
			var req = cli.CreateGetRequest(uri);
			var page = await cli.SendAsync(req, async (ctx) =>
			{
				var status = ctx.Response.StatusCode;
				var responseBody = await ctx.Response.Content.ReadAsByteArrayAsync(ct);
				return new PageResult
				{
					Location = uri,
					Status = status,
					Body = responseBody,
				};
			}, ct);

			this.Page = page;
			return page;
		}

		/// <summary>Navigate to the specified URI, with a POST request</summary>
		public async Task<PageResult> SubmitTo(Uri uri, Slice requestBody, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			var cli = this.GetLocalBetterHttpClient();
			var req = cli.CreateGetRequest(uri);
			var page = await cli.SendAsync(req, async (ctx) =>
			{
				var status = ctx.Response.StatusCode;
				var responseBody = await ctx.Response.Content.ReadAsByteArrayAsync(ct);
				return new PageResult
				{
					Location = uri,
					Status = status,
					Body = responseBody,
				};
			}, ct);

			this.Page = page;
			return page;
		}

	}

	/// <summary>Builder for a <see cref="WebBrowserTestComponent"/></summary>
	public class WebBrowserTestComponentBuilder : IHostTestBuilder
	{

		public string Id => this.Component.Id;

		public required WebBrowserTestComponent Component { get; init; }

		IDistributedTestComponent IHostTestBuilder.Component => this.Component;

		public required IHostTestBuilder Builder { get; init; }

		public IDistributedTestNetworkBuilder Parent => this.Builder.Parent;

		public IDistributedTestContext Context => this.Component.Context;

		public IVirtualNetworkMap Network => this.Component.NetworkMap;

		public VirtualHostIdentity Identity => this.Builder.Identity;

		void IHostTestBuilder.AddSubComponent(IDistributedTestComponent component) => throw new NotSupportedException("Cannot add a sub-component to a web browser");

		internal List<Action<IServiceCollection>> ServiceHandlers { get; } = [ ];

		internal Func<WebBrowserTestComponent, CancellationToken, ValueTask>? StartingHandler { get; set; }

		internal Func<WebBrowserTestComponent, CancellationToken, ValueTask>? StoppingHandler { get; set; }

		internal List<object> Disposables { get; } = [ ];

		public void ConfigureServices(Action<IServiceCollection> handler) => this.ServiceHandlers.Add(handler);

		/// <summary>This instance will be disposed when this host is stopped</summary>
		public void Using(object instance)
		{
			if (instance is not IDisposable or IAsyncDisposable)
			{
				throw new ArgumentException("Instance must either implement IDisposable or IAsyncDisposable");
			}
			this.Disposables.Add(instance);
		}

		public void OnStartup(Action handler) => this.StartingHandler = (host, ct) =>
		{
			if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
			handler();
			return default;
		};

		public void OnStartup(Action<IServiceProvider> handler) => this.StartingHandler = (host, ct) =>
		{
			if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
			handler(host.Services);
			return default;
		};

		public void OnStartup(Func<WebBrowserTestComponent, CancellationToken, ValueTask> handler) => this.StartingHandler = handler;

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

		public void OnShutdown(Action handler) => this.StoppingHandler = (host, ct) =>
		{
			if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
			handler();
			return default;
		};

		public void OnShutdown(Action<WebBrowserTestComponent> handler) => this.StoppingHandler = (host, ct) =>
		{
			if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
			handler(host);
			return default;
		};

		public void OnShutdown(Func<WebBrowserTestComponent, CancellationToken, ValueTask> handler) => this.StoppingHandler = handler;

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

	}

}
