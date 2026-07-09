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

namespace SnowBank.Networking.PacketCapture
{
	using System.Configuration;
	using SnowBank.Networking.Http;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Server.Kestrel.Core;
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Hosting;

	/// <summary>Helper methods for configuration the Package Capture in an application</summary>
	[PublicAPI]
	public static class PacketCaptureDependencyInjectionExtensions
	{

		private static T EnsureProperlyConfigured<T>(IServiceProvider provider)
		{
			var manager = provider.GetService<T>();
			if (manager == null)
			{
				switch(typeof(T).Name)
				{
					case nameof(PacketCaptureManager):
						throw new InvalidOperationException($"{typeof(T).Name} is not registered with the DI container! You must call {nameof(AddPacketCapture)}() on {nameof(IServiceCollection)} during startup.");
					default:
						throw new InvalidOperationException($"{typeof(T).Name} is not registered with the DI container! You must call the appropriate AddXYZ method on {nameof(IServiceCollection)} during startup.");
				}
			}
			return manager;
		}

		/// <summary>Hooks up the Packet Capture infrastructure on the underlying HTTP transport connection</summary>
		public static ListenOptions UsePacketCapture(this ListenOptions listen)
		{
			var manager = EnsureProperlyConfigured<PacketCaptureManager>(listen.ApplicationServices);
			if (manager.Options.Enabled)
			{
				listen.Use((next) =>
				{
					var middleware = new PacketCaptureConnectionMiddleware(manager, next);
					return middleware.OnConnectAsync;
				});
			}
			return listen;
		}

		/// <summary>Registers the Packet Capture infrastructure with the DI</summary>
		public static IServiceCollection AddPacketCapture(this IServiceCollection services, Action<PacketCaptureOptions, IServiceProvider>? configure = null)
		{
#if DEBUG
			if (services.Any(x => x.ServiceType == typeof(IPacketCaptureStore)))
			{
				throw new InvalidOperationException("Packet capture has already been registered!");
			}
#endif

			// Singleton that will hold our packets in memory
			services.AddSingleton<IPacketCaptureStore, PacketCaptureStore>();
			
			services.AddSingleton<PacketCaptureManager>();
			
			services.AddOptions<PacketCaptureOptions>()
				.Configure<IConfiguration, IServiceProvider>((options, config, sp) =>
				{
					// use the application Configuration to set up the capture options...
					var rootSection = config.GetSection("PacketCapture");
					if (rootSection.Exists())
					{
						options.Enabled = rootSection.GetValue("Enabled", false);
						options.AllowedFields = rootSection.GetValue("AllowedFields", CapturedHttpFields.All);

						//TODO: Parsing policy!
						options.CapturePolicy = PacketCapturePolicies.All;

						var sinksSection = rootSection.GetSection("Sinks");
						foreach (var sinkSection in sinksSection.GetChildren())
						{
							string name;
							IConfigurationSection? args;

							if (sinkSection.Value is not null)
							{ // string
								name = sinkSection.Value;
								args = null;
							}
							else
							{
								name = sinkSection.GetValue<string>("Name", "");
								args = sinkSection.GetSection("Args");
							}

							switch (name)
							{
								case "Test":
								{
									// wil register all defined IPacketCaptureSink at runtime
									options.AddAmbientSinks = true;
									break;
								}
								case "Memory":
								{
									options.Sinks.Add(new InMemoryPacketCaptureSink());
									break;
								}
								case "File":
								{
									var fileOptions = args?.Get<FilePacketCaptureOptions>() ?? throw new ConfigurationErrorsException("The 'File' PacketCapture Sink must have a valid 'Args' configuration section.");
									options.Sinks.Add(new FilePacketCaptureSink(fileOptions));
									break;
								}
								case "Console":
								{
									var consoleOptions = args?.Get<ConsolePacketCaptureOptions>() ?? new ConsolePacketCaptureOptions();
									options.Sinks.Add(new ConsolePacketCaptureSink(consoleOptions));
									break;
								}
								case "Trace":
								{
									options.Sinks.Add(new DiagnosticsPacketCaptureSink(debug: false, "Trace"));
									break;
								}
								case "Debug":
								{
									options.Sinks.Add(new DiagnosticsPacketCaptureSink(debug: true, "Debug"));
									break;
								}
								default:
								{
									throw new ConfigurationErrorsException($"Unknown PacketCapture sink with name '{name}' found in configuration.");
								}
							}
						}

						// We add the viewer options
						options.AssetsPath = rootSection.GetValue("AssetsPath", PacketCaptureOptions.DefaultAssetsPath);

						options.CaptureStackTraces = rootSection.GetValue("StackTraces", false);
					}

					// optional user-provided callback
					configure?.Invoke(options, sp);
				}
			);
			services.AddSingleton<PacketCaptureRequestMiddleware>();

			// hook-up the capturing filter for the BetterHttpClient send extensions
			services.AddGlobalHttpFilter<PacketCaptureHttpFilter>();

			// register the in-chain capture handler under the well-known key that the BetterHttp bundles resolve: it is wired as the
			// OUTERMOST handler of every pooled bundle, so capture rides the pipeline and even a bare handler obtained from
			// IHttpMessageHandlerFactory is captured (not just BetterHttpClient sends). Transient so each rebuilt chain gets a fresh one.
			services.AddKeyedTransient<System.Net.Http.DelegatingHandler>(
				BetterHttpClientExtensions.CaptureHandlerServiceKey,
				(sp, _) => new PacketCaptureHandler(sp.GetRequiredService<PacketCaptureManager>()));

			return services;
		}

		/// <summary>Injects the Packet Capture Middleware in this application</summary>
		/// <remarks>All incoming HTTP requests will be captured by the middleware, and processed with the configured <see cref="IPacketCaptureManager"/>></remarks>
		public static IApplicationBuilder UsePacketCapture(this IApplicationBuilder app)
		{
			var manager = EnsureProperlyConfigured<PacketCaptureManager>(app.ApplicationServices);

			if (manager.Options.Enabled)
			{
				if (app.Properties.TryGetValue("PacketCapture:Configured", out _))
				{
					throw new InvalidOperationException("PacketCapture has already been configured on this application!");
				}
				app.Properties["PacketCapture:Configured"] = true;

				app.UseMiddleware<PacketCaptureRequestMiddleware>();

				// we need to hook up the capture with the application start/stop events
				var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
				lifetime.ApplicationStarted.Register(() => manager.Start());
				lifetime.ApplicationStopping.Register(() => manager.PrepareShutdown());
				lifetime.ApplicationStopped.Register(() => manager.Shutdown());
			}

			return app;
		}

	}

}
