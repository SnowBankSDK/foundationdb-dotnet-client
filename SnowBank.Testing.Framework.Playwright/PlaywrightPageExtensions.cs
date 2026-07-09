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

namespace SnowBank.Testing.Framework.Playwright
{
	using Microsoft.Playwright;
	using SnowBank.Diagnostics.Contracts;

	/// <summary>Result of waiting for a page to become ready.</summary>
	/// <param name="Settled">When <see langword="true"/>, the page reached the "DOM ready" state before the deadline expired (phase 1).</param>
	/// <param name="ElapsedMilliseconds">Time actually spent waiting.</param>
	/// <param name="Detail">How the wait ended: <c>"ready"</c> (DOM ready and network quiet), <c>"network-busy"</c> (DOM ready but requests still in flight), or <c>"timeout-dom"</c>.</param>
	public sealed record PageReadyResult(bool Settled, long ElapsedMilliseconds, string Detail);

	/// <summary>"Smart" waits for Playwright pages: wait on a real readiness signal from the browser (DOM state,
	/// application framework startup, network activity) instead of an arbitrary fixed delay.</summary>
	public static class PlaywrightPageExtensions
	{

		/// <summary>Name of the EXPLICIT flag a page can set on <c>window</c> when IT decides it is ready (see remarks on <see cref="WaitForPageReadyAsync"/>).</summary>
		public const string ReadyFlagName = "__snowbankReady";

		/// <summary>Page-side counter of tracked requests currently in flight (maintained by <see cref="NetworkTrackerInitScript"/>).</summary>
		private const string PendingCounterName = "__snowbankPending";

		/// <summary>Page-side timestamp of the last change to the pending counter (maintained by <see cref="NetworkTrackerInitScript"/>).</summary>
		private const string LastChangeName = "__snowbankLastChange";

		/// <summary>Page-side marker preventing the tracker from being installed twice in the same document.</summary>
		private const string TrackerInstalledName = "__snowbankTracker";

		/// <summary>
		/// Init script (re-executed for every new document) that installs a page-side counter of in-flight network
		/// requests. It wraps <c>XMLHttpRequest</c> and <c>fetch</c> to maintain the number of active requests and the
		/// timestamp of the last change. SignalR / hub URLs (long-lived connections that never complete) are IGNORED:
		/// that is precisely what made Playwright's native <c>NetworkIdle</c> unusable in this harness.
		/// </summary>
		public const string NetworkTrackerInitScript = /*lang=javascript*/$$"""
			(() => {
				if (window.{{TrackerInstalledName}}) return;
				window.{{TrackerInstalledName}} = true;
				window.{{PendingCounterName}} = 0;
				window.{{LastChangeName}} = Date.now();
				const bump = (d) => { window.{{PendingCounterName}} = Math.max(0, window.{{PendingCounterName}} + d); window.{{LastChangeName}} = Date.now(); };
				const ignored = (u) => { u = u || ''; return /\/signalr\//i.test(u) || /\/hubs?(\/|$|\?)/i.test(u); };
				const XHR = window.XMLHttpRequest;
				if (XHR && XHR.prototype) {
					const open = XHR.prototype.open, send = XHR.prototype.send;
					XHR.prototype.open = function (m, u) { this.__snowbankUrl = u; return open.apply(this, arguments); };
					XHR.prototype.send = function () {
						if (!ignored(this.__snowbankUrl)) { bump(1); this.addEventListener('loadend', () => bump(-1)); }
						return send.apply(this, arguments);
					};
				}
				const nativeFetch = window.fetch;
				if (nativeFetch) {
					window.fetch = function (input, init) {
						const url = (typeof input === 'string') ? input : (input && input.url) || '';
						if (ignored(url)) return nativeFetch.apply(this, arguments);
						bump(1);
						return nativeFetch.apply(this, arguments).finally(() => bump(-1));
					};
				}
			})();
			""";

		// Phase 1 ("hard" signal, mandatory). The EXPLICIT signal (window flag set to true) wins if the page sets one;
		// otherwise a GENERIC heuristic: DOM complete AND, if ExtJS is present, its startup (onReady) has finished.
		private const string DomReadyPredicate = /*lang=javascript*/$"() => window.{ReadyFlagName} === true || (document.readyState === 'complete' && (!window.Ext || window.Ext.isReady === true))";

		// Phase 2 (best-effort): no tracked request in flight for at least q ms (debounces chained requests).
		private const string NetworkQuietPredicate = /*lang=javascript*/$"(q) => (window.{PendingCounterName} || 0) === 0 && (Date.now() - (window.{LastChangeName} || 0)) >= q";

		/// <summary>
		/// Waits for a page to be "ready" using page-side JS signals instead of a fixed delay. Two phases:
		/// (1) DOM complete plus application-framework startup (mandatory, bounded by <paramref name="timeoutMs"/>);
		/// (2) network quiet (best-effort, bounded by the shorter <paramref name="networkTimeoutMs"/> so a perpetual
		/// poller cannot slow everything down). Adaptive: returns as soon as the page is stable (often well before a
		/// fixed delay would have expired) while still covering slow asynchronous loads.
		/// Never throws on expiry: it returns a non-settled result and lets the caller carry on.
		/// </summary>
		/// <remarks>
		/// <para>The heuristic (DOM + ExtJS + network quiet) is NOT specific to any application: <c>document.readyState</c>
		/// and the XHR/fetch counter are generic, and <c>Ext.isReady</c> is only consulted when ExtJS is present. No change
		/// to the page's own code is required. BUT "network quiet" is only a PROXY for "the app has finished rendering":
		/// for a real SPA (Vue, React, ...) there is no reliable generic "ready" signal.</para>
		/// <para>That is why an EXPLICIT signal takes priority: a page can set <c>window.<see cref="ReadyFlagName"/> = true</c>
		/// when IT decides it is ready (component mounted plus data loaded, first meaningful render, ...), which
		/// short-circuits the heuristic. This is the recommended path for SPA pages.</para>
		/// <para>The network counter only exists in documents where <see cref="NetworkTrackerInitScript"/> was injected
		/// (the <see cref="PlaywrightWebBrowserTestComponent"/> does this for every document of its browser context);
		/// without it, phase 2 sees zero pending requests and settles immediately.</para>
		/// </remarks>
		/// <param name="page">Page to observe.</param>
		/// <param name="ct">Token used to abort the wait (cancellation surfaces as an exception, never as a readiness verdict).</param>
		/// <param name="timeoutMs">Maximum duration of phase 1 (DOM + framework).</param>
		/// <param name="quietMs">Duration of network inactivity required to consider the page stable (debounce).</param>
		/// <param name="networkTimeoutMs">Maximum duration of phase 2 (network quiet), deliberately short.</param>
		public static async Task<PageReadyResult> WaitForPageReadyAsync(this IPage page, CancellationToken ct, int timeoutMs = 10_000, int quietMs = 400, int networkTimeoutMs = 3_000)
		{
			Contract.NotNull(page);
			ct.ThrowIfCancellationRequested();

			var sw = System.Diagnostics.Stopwatch.StartNew();

			try
			{
				await page.WaitForFunctionAsync(DomReadyPredicate, null, new() { Timeout = timeoutMs, PollingInterval = 100 }).WaitAsync(ct).ConfigureAwait(false);
			}
			catch (Exception e) when (e is PlaywrightException || e.GetType().Name is "TimeoutException")
			{
				// Playwright's timeout is not always a PlaywrightException, so we also filter by type name. Never let it leak.
				sw.Stop();
				return new(false, sw.ElapsedMilliseconds, "timeout-dom");
			}

			string detail = "ready";
			try
			{
				await page.WaitForFunctionAsync(NetworkQuietPredicate, quietMs, new() { Timeout = networkTimeoutMs, PollingInterval = 100 }).WaitAsync(ct).ConfigureAwait(false);
			}
			catch (Exception e) when (e is PlaywrightException || e.GetType().Name is "TimeoutException")
			{
				// the network never went quiet (ex: a page with a broken JS loop replaying requests): best-effort, carry on
				detail = "network-busy";
			}

			sw.Stop();
			return new(true, sw.ElapsedMilliseconds, detail);
		}

	}

}
