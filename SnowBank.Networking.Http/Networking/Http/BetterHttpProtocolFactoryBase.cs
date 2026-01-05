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

namespace SnowBank.Networking.Http
{
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Options;

	/// <summary>Base implementation for a <see cref="IBetterHttpClientFactory"/></summary>
	/// <typeparam name="TProtocol">Type of the supported <see cref="IBetterHttpProtocol"/></typeparam>
	/// <typeparam name="TOptions">Type of the <see cref="BetterHttpClientOptions"/> used to configure this protocol</typeparam>
	public abstract class BetterHttpProtocolFactoryBase<TProtocol, TOptions> : IBetterHttpProtocolFactory<TProtocol, TOptions>
		where TProtocol : IBetterHttpProtocol
		where TOptions : BetterHttpClientOptions
	{

		/// <summary>Provider used to create instance of <see cref="TProtocol"/> and its dependencies</summary>
		public IServiceProvider Services { get; }

		protected BetterHttpProtocolFactoryBase(IServiceProvider services)
		{
			this.Services = services;
		}

		/// <summary>Generates the default options for the client</summary>
		protected abstract TOptions CreateOptions();

		/// <summary>Called to post-configure the options</summary>
		protected virtual void OnAfterConfigure(TOptions options)
		{
			//NOP
		}

		/// <inheritdoc />
		//REVIEW: rename to CreateProtocol() ?
		public TProtocol CreateClient(Uri baseAddress, Action<TOptions>? configure = null)
		{
			return CreateClientCore(baseAddress, null, configure);
		}

		/// <inheritdoc />
		//REVIEW: rename to CreateProtocol() or CreateProtocolClient() ?
		public TProtocol CreateClient(Uri baseAddress, HttpMessageHandler handler, Action<TOptions>? configure = null)
		{
			return CreateClientCore(baseAddress, handler, configure);
		}

		private TProtocol CreateClientCore(Uri baseAddress, HttpMessageHandler? handler, Action<TOptions>? configure = null)
		{
			var options = CreateOptions();

			//BUGBUG: REVIEW: any changes we make to the options could be overriden by factory.CreateClient(...) which will also apply its own default options

			var localConfigure = this.Services.GetService<IConfigureOptions<TOptions>>();
			localConfigure?.Configure(options);

			configure?.Invoke(options);

			OnAfterConfigure(options);

			var factory = this.Services.GetRequiredService<IBetterHttpClientFactory>();
			var client = factory.CreateClient(baseAddress, options, handler);
			Contract.Debug.Assert(client != null && client.HostAddress != null && client.Options != null);

			try
			{
				return ActivatorUtilities.CreateInstance<TProtocol>(this.Services, client);
			}
			catch (Exception)
			{
#if DEBUG
				// you forgot to register some of the types used by your custom protocol implementation!
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
				throw;
			}
		}

	}

}
