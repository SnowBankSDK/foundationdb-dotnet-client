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
	using SnowBank.Networking.PacketCapture;

	/// <summary>Simple simulated Host that can be used in unit tests to quickly scaffold some behavior, without having to set up a full-feature host</summary>
	/// <remarks>This host will start very quickly, and can be fully configured to add singletons, custom routes, ...</remarks>
	[PublicAPI]
	public class GenericHostTestComponent : DistributedTestComponent
	{

		/// <summary>Handler called when the host services must be configured</summary>
		public Action<WebApplicationBuilder> ConfigureServicesHandler { get; set; } = (_) => { };

		/// <summary>Handler called when the host application must be configured</summary>
		public Action<WebApplication> ConfigureApplicationHandler { get; set; } = (_) => { };

		/// <summary>Handler called when the host is registered to the virtual network</summary>
		public Action<IVirtualNetworkMap> ConfigureNetworkHandler { get; set; } = (_) => { };

		/// <summary>Handler called when the host starts</summary>
		public Func<CancellationToken, ValueTask> StartingHandler { get; set; } = (_) => default;

		/// <summary>Handler called when the host stops</summary>
		public Func<CancellationToken, ValueTask> StoppingHandler { get; set; } = (_) => default;

		/// <summary>Handler called when the host is disposed</summary>
		public Func<ValueTask> DisposingHandler { get; set; } = () => default;

		/// <summary>Specifies the assembly where any static asset used by this host are located</summary>
		public Assembly? StaticAssetsAssembly { get; set; }

		public GenericHostTestComponent(string id, IVirtualNetworkLocation location, CancellationToken lifetime)
			: base(id, location, lifetime)
		{ }

		protected override void ConfigureServices(WebApplicationBuilder builder)
		{
			this.ConfigureServicesHandler(builder);
		}

		protected override void ConfigureApplication(WebApplication app)
		{
			app.UseVirtualNetworkProxy();
			this.ConfigureApplicationHandler(app);
		}

		protected override Assembly? GetStaticAssetsRuntimeAssembly() => this.StaticAssetsAssembly;

		protected override ValueTask OnStarting(CancellationToken ct)
		{
			var packet = this.Services.GetService<PacketCaptureManager>();
			if (packet != null && !packet.IsRunning) packet.Start();

			return this.StartingHandler(ct);
		}

		protected override ValueTask OnStopping(CancellationToken ct)
		{
			var packet = this.Services.GetService<PacketCaptureManager>();
			if (packet != null && packet.IsRunning) packet.PrepareShutdown();

			return this.StoppingHandler(ct);
		}

		protected override ValueTask OnDisposing()
		{
			var packet = this.Services.GetService<PacketCaptureManager>();
			if (packet != null) packet.Shutdown();

			return this.DisposingHandler();
		}

		protected override void RegisterWithNetwork(IVirtualNetworkMap map)
		{
			this.ConfigureNetworkHandler(map);
		}

	}

}
