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
	using System.Diagnostics.CodeAnalysis;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Options;

	/// <summary>Base implementation of a typed-protocol factory: it creates <typeparamref name="TProtocol"/> instances over plain factory clients.</summary>
	/// <typeparam name="TProtocol">Type of the supported <see cref="IBetterHttpProtocol"/></typeparam>
	/// <typeparam name="TOptions">Type of the <see cref="BetterHttpClientOptions"/> used to configure this protocol</typeparam>
	// The default CreateProtocol resolves TProtocol through ActivatorUtilities, so its public constructor must survive trimming.
	public abstract class BetterHttpProtocolFactoryBase<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProtocol, TOptions> : IBetterHttpProtocolFactory<TProtocol, TOptions>
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
		protected virtual TProtocol CreateProtocol(HttpClient client, TOptions options)
		{
			return ActivatorUtilities.CreateInstance<TProtocol>(this.Services, client);
		}

		/// <inheritdoc />
		//REVIEW: rename to CreateProtocol() ?
		public TProtocol CreateClient(Uri baseAddress, Action<TOptions>? configure = null)
		{
			return CreateClientCore(baseAddress, null, configure);
		}

		/// <summary>Creates a new client for sending requests to a remote target, using a named policy for the pooled pipeline.</summary>
		/// <param name="baseAddress">Host name or IP address of the remote target</param>
		/// <param name="name">Client name whose policy drives the pooled pipeline (TLS, handlers, ...). A registered name carries policy, NOT an origin.</param>
		/// <param name="configure">Handler used to further configure the protocol options</param>
		public TProtocol CreateClient(Uri baseAddress, string name, Action<TOptions>? configure = null)
		{
			Contract.NotNullOrWhiteSpace(name);
			return CreateClientCore(baseAddress, name, configure);
		}

		private TProtocol CreateClientCore(Uri baseAddress, string? name, Action<TOptions>? configure)
		{
			// build the protocol options ONCE (the client name drives the pooled pipeline; these options travel with the protocol object)
			var options = CreateOptions();

			var localConfigure = this.Services.GetService<IConfigureOptions<TOptions>>();
			localConfigure?.Configure(options);

			configure?.Invoke(options);

			OnAfterConfigure(options);

			// per-call configure = protocol/client behavior only; transport policy = the named policy. Fail loudly on the rest:
			// transport policy set here can never reach the shared pooled transport, and silence would be a silent break.
			options.EnsureOnlyProtocolBehavior($"{GetType().Name}.CreateClient");

			// a plain factory client over the name's pooled chain, carrying the protocol options as its per-instance
			// overlay (credentials, hooks, request options, timeout), honored by the SendAsync extensions and the
			// in-chain pipeline handler. The request version/policy stay name-owned (the protocol options' defaults
			// are not an explicit intent).
			var client = BetterHttpClientExtensions.CreateOverlaidClient(this.Services, name ?? BetterHttpClientExtensions.DefaultClientName, baseAddress, options);

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
