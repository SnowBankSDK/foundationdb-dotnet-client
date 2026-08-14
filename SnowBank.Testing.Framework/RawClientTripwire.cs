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
	using System.Collections.Concurrent;
	using System.Diagnostics;
	using System.Net.Http;
	using System.Text;
	using System.Text.RegularExpressions;

	/// <summary>What the <see cref="RawClientTripwire"/> does when a raw (real-socket) request escapes the virtual network.</summary>
	public enum RawClientTripwireAction
	{
		/// <summary>Record and report the egress, but never fail the run. The rollout default: catalogue legitimate infrastructure traffic into the white-list before flipping to <see cref="Fail"/>.</summary>
		Warn = 0,

		/// <summary>Fail the run (via <see cref="RawClientTripwire.Verify"/>) when any non-allowlisted real egress occurred.</summary>
		Fail,
	}

	/// <summary>One real-socket request that escaped the virtual network, with the callstack that opened it.</summary>
	public sealed record RawClientEgress(Uri Uri, string Callstack);

	/// <summary>Detects (and names, by callstack) HTTP requests that open a real socket instead of riding the virtual network: a <c>new HttpClient()</c> with no DI, or a third-party package that sets its own primary handler.</summary>
	/// <remarks>
	/// <para>Mechanism: a <see cref="DiagnosticListener"/> subscriber on <c>System.Net.Http.HttpRequestOut.Start</c>. That event fires only for requests that go through the socket transport, so a request routed through the virtual network is never seen and a fully virtual test stays silent. It fires synchronously in the calling thread at request start, so it carries the target URI and the originating callstack, which is what names the package or method behind a raw client.</para>
	/// <para>This is detection, not prevention: the request still goes out (an OS-level no-egress sandbox would be needed to stop it). It is opt-in per test and, because the listener is process-wide, must run serialized (it cannot attribute a connect to one test under parallel execution). Loopback is allowlisted (the runner's own IPC). Roll out <see cref="RawClientTripwireAction.Warn"/> first so legitimate infrastructure traffic surfaces and gets catalogued into the white-list before flipping to <see cref="RawClientTripwireAction.Fail"/>.</para>
	/// <para>White-list an accepted real endpoint by target host (<see cref="AllowHost"/>, steadier) or by callstack signature (<see cref="AllowCallstack"/>, best-effort: async state machines and inlining can blur frames). A white-listed client really opens a socket, so the test then depends on the real network and belongs in an Explicit/Ignore lane.</para>
	/// </remarks>
	[PublicAPI]
	public sealed class RawClientTripwire : IDisposable
	{
		private const string ListenerName = "HttpHandlerDiagnosticListener";
		private const string RequestStartEvent = "System.Net.Http.HttpRequestOut.Start";

		/// <summary>Arms the tripwire (subscribes to the diagnostic source).</summary>
		/// <param name="action">Whether a non-allowlisted real egress fails the run (via <see cref="Verify"/>) or is only reported. Defaults to <see cref="RawClientTripwireAction.Warn"/> (the rollout default).</param>
		/// <param name="report">Optional sink called once per recorded egress (the URI and callstack), for warn-first logging into the test journal.</param>
		public RawClientTripwire(RawClientTripwireAction action = RawClientTripwireAction.Warn, Action<string>? report = null)
		{
			this.Action = action;
			this.Report = report;
			this.AllListenersSubscription = DiagnosticListener.AllListeners.Subscribe(new ListenerObserver(this));
		}

		private RawClientTripwireAction Action { get; }

		private Action<string>? Report { get; }

		private IDisposable AllListenersSubscription { get; }

		private object Gate { get; } = new();

		private List<IDisposable> Subscriptions { get; } = [ ];

		private ConcurrentQueue<RawClientEgress> Recorded { get; } = new();

		private List<Regex> AllowedHosts { get; } = [ ];

		private List<Regex> AllowedCallstacks { get; } = [ ];

		/// <summary>Real-socket egress recorded so far (a snapshot).</summary>
		public IReadOnlyList<RawClientEgress> Egress => this.Recorded.ToArray();

		/// <summary>Allowlists real egress to a target host (a glob, where <c>*</c> matches any run of characters, e.g. <c>"telemetry.vendor.com"</c> or <c>"telemetry.*.com"</c>). Steadier than a callstack match when the destination is known.</summary>
		public RawClientTripwire AllowHost(string pattern)
		{
			Contract.NotNullOrEmpty(pattern);
			lock (this.Gate) { this.AllowedHosts.Add(GlobToRegex(pattern)); }
			return this;
		}

		/// <summary>Allowlists real egress whose originating callstack matches a glob (e.g. <c>"*SomeVendor.Telemetry.*"</c>). Best-effort: async state machines and inlining can blur frames, so a type/method match usually names the package but is not bulletproof.</summary>
		/// <remarks>The very first request on a fresh <see cref="SocketsHttpHandler"/> builds its handler chain on a threadpool continuation, so that one request keeps its URI but loses the caller frame; the warm requests that follow (the common case for a package that news up a client and calls it repeatedly) keep the frame. Prefer <see cref="AllowHost"/> when the destination is known.</remarks>
		public RawClientTripwire AllowCallstack(string pattern)
		{
			Contract.NotNullOrEmpty(pattern);
			lock (this.Gate) { this.AllowedCallstacks.Add(GlobToRegex(pattern)); }
			return this;
		}

		/// <summary>Waits briefly for any in-flight request-start events to settle, so a test can inspect <see cref="Egress"/> deterministically.</summary>
		public Task DrainAsync(CancellationToken ct) => Task.Delay(150, ct);

		/// <summary>In <see cref="RawClientTripwireAction.Fail"/> mode, throws if any non-allowlisted real egress occurred, naming each URI and its callstack. A no-op in <see cref="RawClientTripwireAction.Warn"/> mode.</summary>
		public void Verify()
		{
			if (this.Action != RawClientTripwireAction.Fail) return;

			var egress = this.Egress;
			if (egress.Count == 0) return;

			var sb = new StringBuilder();
			sb.Append(egress.Count).Append(" HTTP request(s) escaped the virtual network onto a real socket. Route them through INetworkMap, mock them, or white-list them into an Explicit/Ignore lane:");
			foreach (var e in egress)
			{
				sb.AppendLine().Append(" - ").Append(e.Uri).AppendLine().Append("   from:").AppendLine().Append(e.Callstack);
			}
			throw new InvalidOperationException(sb.ToString());
		}

		private void OnRequestStart(Uri? uri)
		{
			if (uri is null || uri.IsLoopback) return; // loopback is the runner's own IPC

			lock (this.Gate)
			{
				foreach (var rx in this.AllowedHosts)
				{
					if (rx.IsMatch(uri.Host)) return;
				}
			}

			var callstack = new StackTrace(fNeedFileInfo: false).ToString();

			lock (this.Gate)
			{
				foreach (var rx in this.AllowedCallstacks)
				{
					if (rx.IsMatch(callstack)) return;
				}
			}

			this.Recorded.Enqueue(new RawClientEgress(uri, callstack));
			this.Report?.Invoke($"raw-client egress escaped the virtual network: {uri}{Environment.NewLine}{callstack}");
		}

		/// <inheritdoc />
		public void Dispose()
		{
			lock (this.Gate)
			{
				foreach (var sub in this.Subscriptions) sub.Dispose();
				this.Subscriptions.Clear();
			}
			this.AllListenersSubscription.Dispose();
		}

		/// <summary>Translates a simple glob (only <c>*</c> is special) into an anchored, case-insensitive regex; <c>.</c> matches a newline so a callstack pattern can span frames.</summary>
		private static Regex GlobToRegex(string pattern)
			=> new("^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

		private sealed class ListenerObserver : IObserver<DiagnosticListener>
		{
			private RawClientTripwire Owner { get; }

			public ListenerObserver(RawClientTripwire owner) => this.Owner = owner;

			public void OnNext(DiagnosticListener dl)
			{
				if (dl.Name != ListenerName) return;
				var sub = dl.Subscribe(new EventObserver(this.Owner));
				lock (this.Owner.Gate) { this.Owner.Subscriptions.Add(sub); }
			}

			public void OnCompleted() { }

			public void OnError(Exception error) { }
		}

		private sealed class EventObserver : IObserver<KeyValuePair<string, object?>>
		{
			private RawClientTripwire Owner { get; }

			public EventObserver(RawClientTripwire owner) => this.Owner = owner;

			public void OnNext(KeyValuePair<string, object?> kv)
			{
				if (kv.Key != RequestStartEvent) return;
				var uri = (kv.Value?.GetType().GetProperty("Request")?.GetValue(kv.Value) as HttpRequestMessage)?.RequestUri;
				this.Owner.OnRequestStart(uri);
			}

			public void OnCompleted() { }

			public void OnError(Exception error) { }
		}

	}

}
