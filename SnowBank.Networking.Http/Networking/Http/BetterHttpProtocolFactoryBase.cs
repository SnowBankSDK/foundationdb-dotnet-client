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

		/// <summary>Creates the protocol instance, handing it the client and the resolved options.</summary>
		/// <remarks>The default resolves the protocol from the DI container with the client as a parameter. Override this to hand the protocol its <typeparamref name="TOptions"/> explicitly (so it does not have to reach back into the client instance).</remarks>
		protected virtual TProtocol CreateProtocol(BetterHttpClient client, TOptions options)
		{
			return ActivatorUtilities.CreateInstance<TProtocol>(this.Services, client);
		}

		/// <inheritdoc />
		//REVIEW: rename to CreateProtocol() ?
		public TProtocol CreateClient(Uri baseAddress, Action<TOptions>? configure = null)
		{
			return CreateClientCore(baseAddress, null, null, configure);
		}

		/// <summary>Creates a new client for sending requests to a remote target, using the named policy bundle for the pooled pipeline.</summary>
		/// <param name="baseAddress">Host name or IP address of the remote target</param>
		/// <param name="name">Name of the policy bundle to use for the pooled pipeline (TLS, filters, ...). A registered name is a bundle, NOT an origin.</param>
		/// <param name="configure">Handler used to further configure the protocol options</param>
		public TProtocol CreateClient(Uri baseAddress, string name, Action<TOptions>? configure = null)
		{
			Contract.NotNullOrWhiteSpace(name);
			return CreateClientCore(baseAddress, null, name, configure);
		}

		/// <inheritdoc />
		//REVIEW: rename to CreateProtocol() or CreateProtocolClient() ?
		public TProtocol CreateClient(Uri baseAddress, HttpMessageHandler handler, Action<TOptions>? configure = null)
		{
			return CreateClientCore(baseAddress, handler, null, configure);
		}

		private TProtocol CreateClientCore(Uri baseAddress, HttpMessageHandler? handler, string? name, Action<TOptions>? configure)
		{
			// build the protocol options ONCE (the named bundle drives the pooled pipeline; these options travel with the protocol object)
			var options = CreateOptions();

			var localConfigure = this.Services.GetService<IConfigureOptions<TOptions>>();
			localConfigure?.Configure(options);

			configure?.Invoke(options);

			OnAfterConfigure(options);

			var factory = this.Services.GetRequiredService<IBetterHttpClientFactory>();
			BetterHttpClient client;
			if (handler is not null)
			{ // transitional: a caller-provided transport carries the protocol options through a one-shot pipeline
#pragma warning disable CS0618 // per-call options path, retired when protocol pipelines fully move to named bundles
				client = factory.CreateClient(baseAddress, options, handler);
#pragma warning restore CS0618
			}
			else
			{ // pooled shell over the named (or default) policy bundle
				// per-call configure = protocol/client behavior only; wire policy = the bundle. Fail loudly on the rest:
				// under the retired one-shot path a per-call TLS callback or filter used to work, so silence would be a break.
				options.EnsureOnlyProtocolBehavior($"{GetType().Name}.CreateClient");

				// the shell tier of the protocol options rides the transient shell (per-connection auth headers etc.);
				// the request version/policy stay bundle-owned (the protocol options' defaults are not an explicit intent).
				client = factory.CreateClient(baseAddress, new BetterHttpShellOptions()
				{
					DefaultRequestHeaders = options.DefaultRequestHeaders,
					RequestOptions = options.Options,
					Hooks = options.Hooks,
				}, name);
			}
			Contract.Debug.Assert(client != null);

			try
			{
				return CreateProtocol(client, options);
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
