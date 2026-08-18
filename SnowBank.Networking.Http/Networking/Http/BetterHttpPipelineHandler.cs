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

	/// <summary>In-chain handler that runs the BetterHttp request stages (filters, credentials, hooks) for every consumer of a pooled chain.</summary>
	/// <remarks>
	/// <para>This handler replaces the previous <c>MagicalHandler</c>, which only acted when the request carried a <see cref="BetterHttpClientContext"/> attached by the send extensions; a plain <see cref="HttpClient"/> obtained from <see cref="System.Net.Http.IHttpClientFactory"/> silently skipped every stage. This handler is built per client name, holds that name's resolved options, and creates the context itself when the request arrives without one, so the request stages run for every door: a typed or keyed client, a factory client, a bare handler, or a <see cref="BetterHttpRequestExtensions.SendAsync{TResult}(System.Net.Http.HttpClient,System.Net.Http.HttpRequestMessage,System.Func{BetterHttpClientContext,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)">send-extension</see> call.</para>
	/// <para>When the request already carries a context (attached by the send extensions), the handler fills in the parts the extension could not know (the resolved options and the service provider) unless the extension already resolved them from a factory-built shell, whose per-shell overlay (headers, hooks, per-request credentials) must win over the chain's own options.</para>
	/// <para>The handler must run once the <see cref="HttpClient"/> has fully set up the request (default headers added), which is why the stages live in a delegating handler instead of the client.</para>
	/// </remarks>
	internal sealed class BetterHttpPipelineHandler : DelegatingHandler
	{

		/// <summary>Creates a handler for one client name's pooled chain.</summary>
		/// <param name="name">Name of the client (used as the prefix of per-request correlation ids).</param>
		/// <param name="options">Resolved options for this name (the full layer merge), captured when the chain is built.</param>
		/// <param name="services">Service provider used by filters and credentials.</param>
		/// <param name="clock">Clock used to timestamp the requests that this handler creates a context for.</param>
		/// <param name="timeProvider">Time source used to enforce the per-request <see cref="BetterHttpClientOptions.Timeout"/> (the system time provider in production, a fake one inside a test that simulates the passage of time).</param>
		public BetterHttpPipelineHandler(string name, BetterHttpClientOptions options, IServiceProvider services, IClock clock, TimeProvider timeProvider)
		{
			Contract.NotNull(name);
			Contract.NotNull(options);
			Contract.NotNull(services);
			Contract.NotNull(clock);
			Contract.NotNull(timeProvider);
			this.Name = name;
			this.Options = options;
			this.Services = services;
			this.Clock = clock;
			this.TimeProvider = timeProvider;
			this.Id = CorrelationIdGenerator.GetNextId();
		}

		/// <summary>Name of the client whose chain this handler rides.</summary>
		private string Name { get; }

		/// <summary>Resolved options for this client name (the full layer merge).</summary>
		private BetterHttpClientOptions Options { get; }

		/// <summary>Service provider used by filters and credentials.</summary>
		private IServiceProvider Services { get; }

		/// <summary>Clock used to timestamp requests.</summary>
		private IClock Clock { get; }

		/// <summary>Time source used to enforce the per-request timeout.</summary>
		private TimeProvider TimeProvider { get; }

		/// <summary>Unique id of this handler instance, used as the prefix of the correlation ids it generates.</summary>
		private string Id { get; }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			bool ownsStages;
			if (!request.Options.TryGetValue(BetterHttpRequestExtensions.OptionKey, out var context))
			{ // a plain send (GetAsync, PostAsync, SendAsync) from any door: create the context here, so the stages run anyway
				context = new BetterHttpClientContext()
				{
					Id = BetterHttpRequestExtensions.NewRequestId(this.Id),
					Options = this.Options,
					Clock = this.Clock,
					Services = this.Services,
					Cancellation = cancellationToken,
					State = new(StringComparer.Ordinal),
					Request = request,
					CreatedAt = this.Clock.GetCurrentInstant(),
				};
				request.Options.Set(BetterHttpRequestExtensions.OptionKey, context);
				ownsStages = true;
			}
			else if (!context.HasResolvedOptions)
			{ // attached by the send extensions over a client that carried no runtime: fill in what the extension could not know
				context.Options = this.Options;
				context.Services = this.Services;
				context.HasResolvedOptions = true;
				ownsStages = true;
			}
			else
			{ // attached by the send extensions over a factory-built shell: the shell's overlay options win, and the extension
			  // already ran the Configure stage with them; this handler only runs the request stages below.
				ownsStages = false;
			}

			var options = context.Options;

			if (ownsStages)
			{ // the Configure stage used to run in the send extensions; for a context created (or completed) here, it runs here.
				context.SetStage(BetterHttpClientStage.Configure);
				foreach (var filter in options.Filters)
				{
					try
					{
						await filter.Configure(context).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						if (!(options.Hooks?.OnFilterError(context, e) ?? false))
						{
							throw;
						}
					}
				}
				options.Hooks?.OnConfigured(context);
			}

			context.SetStage(BetterHttpClientStage.PrepareRequest);

			// inject any custom request option
			if (options.Options is not null)
			{
				IDictionary<string, object?> dict = request.Options;
				foreach (var kv in options.Options)
				{
					dict.Add(kv);
				}
			}

			// notify all filters
			foreach (var filter in options.Filters)
			{
				try
				{
					await filter.PrepareRequest(context).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					if (!(options.Hooks?.OnFilterError(context, e) ?? false))
					{
						throw;
					}
				}
			}

			// handle authentication (the credentials see the final request, default headers included)
			if (options.Credentials is not null)
			{
				await options.Credentials.OnBeforeRequest(context).ConfigureAwait(false);
			}

			options.Hooks?.OnRequestPrepared(context);

			context.SetStage(BetterHttpClientStage.Send);
			HttpResponseMessage res;
			if (options.Timeout is { } timeout)
			{ // enforced here, against the host's TimeProvider, so a fake time provider can trigger it by advancing time
				using var timeoutCts = new CancellationTokenSource(timeout, this.TimeProvider);
				using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
				try
				{
					res = await base.SendAsync(request, linked.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
				{ // same shape as HttpClient.Timeout: a TaskCanceledException whose inner exception is a TimeoutException
					throw new TaskCanceledException($"The request was canceled due to the configured Timeout of {timeout.TotalSeconds} seconds elapsing.", new TimeoutException($"A task was canceled after {timeout.TotalSeconds} seconds."), timeoutCts.Token);
				}
			}
			else
			{
				res = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
			}

			context.SetStage(BetterHttpClientStage.CompleteRequest);
			foreach (var filter in options.Filters)
			{
				try
				{
					await filter.CompleteRequest(context).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					if (!(options.Hooks?.OnFilterError(context, e) ?? false))
					{
						throw;
					}
				}
			}

			options.Hooks?.OnRequestCompleted(context);

			//note: handling of the response is deferred to the caller!
			return res;
		}

	}

}
