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

//#define FULL_DEBUG

namespace SnowBank.Networking.Http
{
	using System.IO;
	using System.Linq.Expressions;
	using System.Net.Security;
	using System.Net.Sockets;
	using System.Reflection;
	using System.Runtime.ExceptionServices;
	using System.Security.Authentication;
	using System.Security.Cryptography;
	using System.Security.Cryptography.X509Certificates;
	using System.Threading.Tasks;

	/// <summary>Less restrictive wrapper over <see cref="System.Net.Http.HttpClientHandler"/>, that exposes protected or internal members</summary>
	[PublicAPI]
	public class BetterHttpClientHandler : HttpMessageHandler
	{

		// Issues with HttpMessageHandler:
		// - The ctor does not accept a SocketsHttpHandler directly, and we cannot access the one that it creates under the hood.
		// - We cannot call any of the Send/SendAsync methods, since they are not public.
		// This type's purpose is to make all these fields and methods public, as well as customize the SocketsHttpHandler

		/// <summary>Underlying <see cref="SocketsHttpHandler"/> that will perform the HTTP request using sockets</summary>
		public SocketsHttpHandler Sockets { get; }

		/// <summary>Map of the network used to local the remote target</summary>
		/// <remarks>This is used to emulate virtual hosts, and/or inject artificial errors/latency during testing.</remarks>
		public INetworkMap Network { get; }

		private volatile bool m_disposed;

		#region Cheat Codes...

		/// <summary>Helper class that invokes any "protected internal" methods inside <see cref="SocketsHttpHandler"/></summary>
		private static class CheatCodes
		{

			private static Func<SocketsHttpHandler, HttpRequestMessage, CancellationToken, HttpResponseMessage> GetSendHandler()
			{
				var method = typeof(SocketsHttpHandler).GetMethod("Send", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (method == null)
				{
#if DEBUG
					if (Debugger.IsAttached) Debugger.Break();
#endif
					throw new NotSupportedException("Could not find SocketsHttpHandler.Send(). This may be caused by a new version of the .NET Framework that is not supported by this library!");
				}

				var prmHandler = Expression.Parameter(typeof(SocketsHttpHandler), "handler");
				var prmMessage = Expression.Parameter(typeof(HttpRequestMessage), "message");
				var prmCancellationToken = Expression.Parameter(typeof(CancellationToken), "ct");
				var body = Expression.Call(prmHandler, method, prmMessage, prmCancellationToken);

				var lambda = Expression.Lambda<Func<SocketsHttpHandler, HttpRequestMessage, CancellationToken, HttpResponseMessage>>(
					body,
					"SocketsHttpHandler_Send",
					tailCall: true,
					[ prmHandler, prmMessage, prmCancellationToken ]
				);
				return lambda.Compile();
			}

			private static Func<SocketsHttpHandler, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> GetSendAsyncHandler()
			{
				var method = typeof(SocketsHttpHandler).GetMethod("SendAsync", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (method == null)
				{
#if DEBUG
					if (Debugger.IsAttached) Debugger.Break();
#endif
					throw new NotSupportedException("Could not find SocketsHttpHandler.SendAsync(). This may be caused by a new version of the .NET Framework that is not supported by this library!");
				}

				// lambda(SocketsHttpHandler handler, HttpRequestMessage message, CancellationToken ct) => handler.SendAsync(message, ct);

				var prmHandler = Expression.Parameter(typeof(SocketsHttpHandler), "handler");
				var prmMessage = Expression.Parameter(typeof(HttpRequestMessage), "message");
				var prmCancellationToken = Expression.Parameter(typeof(CancellationToken), "ct");
				var body = Expression.Call(prmHandler, method, prmMessage, prmCancellationToken);

				var lambda = Expression.Lambda<Func<SocketsHttpHandler, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>(
					body,
					"SocketsHttpHandler_SendAsync",
					tailCall: true,
					[ prmHandler, prmMessage, prmCancellationToken ]
				);
				return lambda.Compile();
			}

			public static readonly Func<SocketsHttpHandler, HttpRequestMessage, CancellationToken, HttpResponseMessage> SendInvoker = GetSendHandler();

			public static readonly Func<SocketsHttpHandler, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> SendAsyncInvoker = GetSendAsyncHandler();

		}

		public static readonly bool HasCheatCodes = TestCheatCodes();

		private static bool TestCheatCodes()
		{
			try
			{
				_ = CheatCodes.SendAsyncInvoker;
				return true;
			}
			catch (Exception)
			{
#if DEBUG
				// If you end up here, it means that the reflection logic failed, meaning that the internals of SocketsHttpHandler have changed!
				// => inspect what changed inside "SocketsHttpHandler.Send/SendAsync" and fix things accordingly!
				if (Debugger.IsAttached) Debugger.Break();
#endif
				return false;
			}
		}

		#endregion

		/// <summary>Constructs a <see cref="BetterHttpClientHandler"/></summary>
		/// <param name="map">Map of the network, used to locate and resolve remote host addresses</param>
		public BetterHttpClientHandler(INetworkMap map)
			: this(map, new SocketsHttpHandler())
		{ }

		/// <summary>Constructs a <see cref="BetterHttpClientHandler"/> with a custom handler</summary>
		/// <param name="map">Map of the network, used to locate and resolve remote host addresses</param>
		/// <param name="sockets">Handler that has already been constructed</param>
		public BetterHttpClientHandler(INetworkMap map, SocketsHttpHandler sockets)
		{
			Contract.NotNull(map);
			Contract.NotNull(sockets);

			this.Network = map;
			this.Sockets = sockets;
			this.ClientCertificateOptions = ClientCertificateOption.Manual;
		}

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
			if (disposing && !m_disposed)
			{
				m_disposed = true;
				this.Sockets.Dispose();
			}

			base.Dispose(disposing);
		}
		

		/// <summary>Gets or sets a value that indicates whether the handler supports automatic decompression of response bodies.</summary>
		//note: internal const SocketsHttpHandler.SupportsAutomaticDecompression is "true"
		public virtual bool SupportsAutomaticDecompression { get; set; } = true;

		/// <summary>Gets or sets a value that indicates whether the handler supports the use proxies.</summary>
		//note: internal const SocketsHttpHandler.SupportsProxy is "true"
		public virtual bool SupportsProxy { get; set; } = true;

		/// <summary>Gets or sets a value that indicates whether the handler supports automatic following of redirections.</summary>
		//note: internal const SocketsHttpHandler.SupportsRedirectConfiguration is "true"
		public virtual bool SupportsRedirectConfiguration { get; set; } = true;

		/// <inheritdoc cref="SocketsHttpHandler.UseCookies"/>
		public bool UseCookies
		{
			get => this.Sockets.UseCookies;
			set => this.Sockets.UseCookies = value;
		}
		
		/// <inheritdoc cref="SocketsHttpHandler.CookieContainer"/>
		public CookieContainer CookieContainer
		{
			get => this.Sockets.CookieContainer;
			set
			{
				ArgumentNullException.ThrowIfNull(value);

				this.Sockets.CookieContainer = value;
			}
		}

		/// <inheritdoc cref="SocketsHttpHandler.AutomaticDecompression"/>
		public DecompressionMethods AutomaticDecompression
		{
			get => this.Sockets.AutomaticDecompression;
			set => this.Sockets.AutomaticDecompression = value;
		}

		/// <inheritdoc cref="SocketsHttpHandler.UseProxy"/>
		public bool UseProxy
		{
			get => this.Sockets.UseProxy;
			set => this.Sockets.UseProxy = value;
		}

		/// <inheritdoc cref="SocketsHttpHandler.Proxy"/>
		public IWebProxy? Proxy
		{
			get => this.Sockets.Proxy;
			set => this.Sockets.Proxy = value;
		}

		/// <inheritdoc cref="SocketsHttpHandler.DefaultProxyCredentials"/>
		public ICredentials? DefaultProxyCredentials
		{
			get => this.Sockets.DefaultProxyCredentials;
			set => this.Sockets.DefaultProxyCredentials = value;
		}

		/// <inheritdoc cref="SocketsHttpHandler.PreAuthenticate"/>
		public bool PreAuthenticate
		{
			get => this.Sockets.PreAuthenticate;
			set => this.Sockets.PreAuthenticate = value;
		}

		/// <summary>Gets of sets a value that indicates whether the <see cref="CredentialCache.DefaultCredentials"/> should be used by this request.</summary>
		public bool UseDefaultCredentials
		{
			// SocketsHttpHandler doesn't have a separate UseDefaultCredentials property.  There
			// is just a Credentials property.  So, we need to map the behavior.
			get => this.Sockets.Credentials == CredentialCache.DefaultCredentials;
			set
			{
				if (value)
				{
					this.Sockets.Credentials = CredentialCache.DefaultCredentials;
				}
				else
				{
					if (this.Sockets.Credentials == CredentialCache.DefaultCredentials)
					{
						// Only clear out the Credentials property if it was a DefaultCredentials.
						this.Sockets.Credentials = null;
					}
				}
			}
		}

		/// <inheritdoc cref="SocketsHttpHandler.Credentials"/>
		public ICredentials? Credentials
		{
			get => this.Sockets.Credentials;
			set => this.Sockets.Credentials = value;
		}

		/// <inheritdoc cref="SocketsHttpHandler.AllowAutoRedirect"/>
		public bool AllowAutoRedirect
		{
			get => this.Sockets.AllowAutoRedirect;
			set => this.Sockets.AllowAutoRedirect = value;
		}

		/// <inheritdoc cref="SocketsHttpHandler.MaxAutomaticRedirections"/>
		public int MaxAutomaticRedirections
		{
			get => this.Sockets.MaxAutomaticRedirections;
			set => this.Sockets.MaxAutomaticRedirections = value;
		}

		/// <inheritdoc cref="SocketsHttpHandler.MaxConnectionsPerServer"/>
		public int MaxConnectionsPerServer
		{
			get => this.Sockets.MaxConnectionsPerServer;
			set => this.Sockets.MaxConnectionsPerServer = value;
		}

		public long MaxRequestContentBufferSize
		{
			// This property is not supported. In the .NET Framework it was only used when the handler needed to
			// automatically buffer the request content. That only happened if neither 'Content-Length' nor
			// 'Transfer-Encoding: chunked' request headers were specified. So, the handler thus needed to buffer
			// in the request content to determine its length and then would choose 'Content-Length' semantics when
			// POST'ing. In .NET Core, the handler will resolve the ambiguity by always choosing
			// 'Transfer-Encoding: chunked'. The handler will never automatically buffer in the request content.

			get => 0; // Returning zero is appropriate since in .NET Framework it means no limit.

			set
			{
				if (value < 0)
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				const long MAX_BUFFER_SIZE = int.MaxValue; //TODO: HttpContent.MaxBufferSize
				if (value > MAX_BUFFER_SIZE) 
				{
					throw new ArgumentOutOfRangeException(nameof(value), value, $"Buffering more than {MAX_BUFFER_SIZE:N0} bytes is not supported.");
				}

				CheckDisposed();

				// No-op on property setter.
			}
		}

		/// <inheritdoc cref="SocketsHttpHandler.MaxResponseHeadersLength"/>
		public int MaxResponseHeadersLength
		{
			get => this.Sockets.MaxResponseHeadersLength;
			set => this.Sockets.MaxResponseHeadersLength = value;
		}

		public ClientCertificateOption ClientCertificateOptions
		{
			get;
			set
			{
				switch (value)
				{
					case ClientCertificateOption.Manual:
					{
						ThrowForModifiedManagedSslOptionsIfStarted();
						field = value;
						this.Sockets.SslOptions.LocalCertificateSelectionCallback = (_, _, _, _, _) => CertHelpers.GetEligibleClientCertificate(this.Sockets.SslOptions.ClientCertificates)!;
						break;
					}
					case ClientCertificateOption.Automatic:
					{
						ThrowForModifiedManagedSslOptionsIfStarted();
						field = value;
						this.Sockets.SslOptions.LocalCertificateSelectionCallback = (_, _, _, _, _) => CertHelpers.GetEligibleClientCertificate()!;
						break;
					}
					default:
					{
						throw new ArgumentOutOfRangeException(nameof(value));
					}
				}
			}
		}

		/// <summary>Helper for dealing with client certificates</summary>
		private static class CertHelpers
		{

			private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

			internal static X509Certificate2? GetEligibleClientCertificate()
			{
				// Get initial list of client certificates from the MY store.
				X509Certificate2Collection candidateCerts;
				using (var myStore = new X509Store(StoreName.My, StoreLocation.CurrentUser))
				{
					myStore.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);
					candidateCerts = myStore.Certificates;
				}

				return GetEligibleClientCertificate(candidateCerts);
			}

			internal static X509Certificate2? GetEligibleClientCertificate(X509CertificateCollection? candidateCerts)
			{
				if (candidateCerts == null || candidateCerts.Count == 0)
				{
					return null;
				}

				var certs = new X509Certificate2Collection();
				certs.AddRange(candidateCerts);

				return GetEligibleClientCertificate(certs);
			}

			private static X509Certificate2? GetEligibleClientCertificate(X509Certificate2Collection? candidateCerts)
			{
				if (candidateCerts == null || candidateCerts.Count == 0)
				{
					return null;
				}

				foreach (X509Certificate2 cert in candidateCerts)
				{
					if (!cert.HasPrivateKey)
					{
						continue;
					}

					if (IsValidClientCertificate(cert))
					{
						return cert;
					}
				}

				return null;
			}

			private static bool IsValidClientCertificate(X509Certificate2 cert)
			{
				foreach (X509Extension extension in cert.Extensions)
				{
					switch (extension)
					{
						case X509EnhancedKeyUsageExtension eku when !IsValidForClientAuthenticationEku(eku):
						case X509KeyUsageExtension ku when !IsValidForDigitalSignatureUsage(ku):
						{
							return false;
						}
					}
				}
				return true;
			}

			private static bool IsValidForClientAuthenticationEku(X509EnhancedKeyUsageExtension eku)
			{
				foreach (Oid oid in eku.EnhancedKeyUsages)
				{
					if (oid.Value == CertHelpers.ClientAuthenticationOid)
					{
						return true;
					}
				}

				return false;
			}

			private static bool IsValidForDigitalSignatureUsage(X509KeyUsageExtension ku)
			{
				return (ku.KeyUsages & X509KeyUsageFlags.DigitalSignature) == X509KeyUsageFlags.DigitalSignature;
			}
		}

		/// <summary>Gets the <see cref="X509CertificateCollection"/> that this client will use when asked to authenticate by the remote server</summary>
		public X509CertificateCollection ClientCertificates
		{
			get
			{
				if (this.ClientCertificateOptions != ClientCertificateOption.Manual)
				{
					throw new InvalidOperationException($"The {nameof(ClientCertificateOptions)} property must be set to '{nameof(ClientCertificateOption.Manual)}' to use this property.");
				}

				return this.Sockets.SslOptions.ClientCertificates ??= new X509CertificateCollection();
			}
		}

		/// <summary>Gets or sets the callback used to validate the certificate provided by the remote server</summary>
		/// <remarks>
		/// <para>The callback validates a <em>connection</em> (there is no request at TLS-handshake time), so it receives only the certificate, the chain and the policy errors. It is mapped directly onto <see cref="SslClientAuthenticationOptions.RemoteCertificateValidationCallback"/>.</para>
		/// </remarks>
		public Func<X509Certificate2?, X509Chain?, SslPolicyErrors, bool>? ServerCertificateCustomValidationCallback
		{
			get;
			set
			{
				ThrowForModifiedManagedSslOptionsIfStarted();
				field = value;
				this.Sockets.SslOptions.RemoteCertificateValidationCallback = value is null ? null
					: (_, cert, chain, errors) => value(cert switch { null => null, X509Certificate2 c2 => c2, var c => new X509Certificate2(c) }, chain, errors);
			}
		}

		/// <summary>Gets or sets the certificate verification mode used when validating the certificate provided by the remote server</summary>
		public bool CheckCertificateRevocationList
		{
			get => this.Sockets.SslOptions.CertificateRevocationCheckMode == X509RevocationMode.Online;
			set
			{
				ThrowForModifiedManagedSslOptionsIfStarted();
				this.Sockets.SslOptions.CertificateRevocationCheckMode = value ? X509RevocationMode.Online : X509RevocationMode.NoCheck;
			}
		}

		/// <summary>Gets or sets the value that represents the protocol versions offered by the client to the server during authentication.</summary>
		public SslProtocols SslProtocols
		{
			get => this.Sockets.SslOptions.EnabledSslProtocols;
			set
			{
				ThrowForModifiedManagedSslOptionsIfStarted();
				this.Sockets.SslOptions.EnabledSslProtocols = value;
			}
		}

		/// <inheritdoc cref="SocketsHttpHandler.Properties"/>
		public IDictionary<string, object?> Properties => this.Sockets.Properties;

		protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken ct) => CheatCodes.SendInvoker(Sockets, request, ct);

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => CheatCodes.SendAsyncInvoker(Sockets, request, ct);

		/// <summary>Invokes the <see cref="HttpMessageHandler.Send"/> internal method of the wrapped handler</summary>
		/// <remarks>This method is marked "protected internal" and would not be accessible otherwise.</remarks>
		public HttpResponseMessage InvokeSend(HttpRequestMessage request, CancellationToken ct) => this.Send(request, ct);

		/// <summary>Invokes the <see cref="HttpMessageHandler.SendAsync"/> intenral method of the wrapped handler</summary>
		/// <remarks>This method is marked "protected internal" and would not be accessible otherwise.</remarks>
		public Task<HttpResponseMessage> InvokeSendAsync(HttpRequestMessage request, CancellationToken ct) => this.SendAsync(request, ct);

		/// <summary>Certificate validation callback that always returns <c>true</c></summary>
		/// <remarks>Use with caution! This is only intended for testing, or during local development with self-signed certificates.</remarks>
		public static readonly Func<X509Certificate2?, X509Chain?, SslPolicyErrors, bool> DangerousAcceptAnyServerCertificateValidator = static (_, _, _) => true;

		private void ThrowForModifiedManagedSslOptionsIfStarted()
		{
			// Hack to trigger an InvalidOperationException if a property that's stored on
			// SslOptions is changed, since SslOptions itself does not do any such checks.
			this.Sockets.SslOptions = this.Sockets.SslOptions;
		}

		private void CheckDisposed()
		{
			ObjectDisposedException.ThrowIf(m_disposed, this);
		}

		private Socket CreateSocket()
		{
			var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
			//TODO: custom settings?
			return socket;
		}

		private static IPAddress? GetLocalEndpointAddress(Socket socket)
		{
			var ip = socket.LocalEndPoint is IPEndPoint ipEndpoint ? ipEndpoint.Address : null;
			if (ip == null) return null;
			if (ip.IsIPv4MappedToIPv6)
			{
				//note: if we have "::ffff:192.168.1.23", we will convert it to 192.168.1.23
				ip = ip.MapToIPv4();
			}
			return ip;
		}

		public ValueTask<Stream> SocketsConnectCallback(SocketsHttpConnectionContext context, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return new ValueTask<Stream>(Task.FromCanceled<Stream>(ct));

			// grab the BetterHttpClientContext (if there is one)
			context.InitialRequestMessage.Options.TryGetValue(BetterHttpRequestExtensions.OptionKey, out var clientContext);

			if (IPAddress.TryParse(context.DnsEndPoint.Host, out var address))
			{ // already an IP address, we can connect directly to the remote socket
				return ConnectToSingleAsync(clientContext, new IPEndPoint(address, context.DnsEndPoint.Port), context.DnsEndPoint.Host, ct);
			}

			// we will need to resolve the hostname using DNS before connecting!
			return ConnectToHostNameAsync(clientContext, context.DnsEndPoint, ct);
		}

		/// <summary>Connects to a remote host via a single endpoint.</summary>
		private async ValueTask<Stream> ConnectToSingleAsync(BetterHttpClientContext? context, IPEndPoint endpoint, string? hostName, CancellationToken ct)
		{
			context?.SetStage(BetterHttpClientStage.Connecting);
			var socket = CreateSocket();
			var sw = Stopwatch.StartNew();
			try
			{
				await socket.ConnectAsync(endpoint, ct).ConfigureAwait(false);
				sw.Stop();
				this.Network.RecordEndpointConnectionAttempt(endpoint, sw.Elapsed, null, hostName, GetLocalEndpointAddress(socket));
				context?.Options.Hooks?.OnSocketConnected(context, socket);
				return new NetworkStream(socket);
			}
			catch (Exception e)
			{
				sw.Stop();
				this.Network.RecordEndpointConnectionAttempt(endpoint, sw.Elapsed, e, hostName, localAddress: null);
				context?.Options.Hooks?.OnSocketFailed(context, socket, e);
				socket.Dispose();
				throw;
			}
		}

		/// <summary>Attempts a connection to the socket of the remote host</summary>
		private async Task<Socket> AttemptConnectAsync(IPEndPoint ep, CancellationToken ct)
		{
			var socket = CreateSocket();
			try
			{
				await socket.ConnectAsync(ep, ct).ConfigureAwait(false);
				return socket;
			}
			catch (Exception)
			{
				socket.Dispose();
				throw;
			}
		}

		/// <summary>Resolves the host name of the remote host, then connect to any of the matching IP addresses available</summary>
		private async ValueTask<Stream> ConnectToHostNameAsync(BetterHttpClientContext? clientContext, DnsEndPoint endpoint, CancellationToken ct)
		{
			var entries = await this.Network.DnsLookup(endpoint.Host, endpoint.AddressFamily, ct).ConfigureAwait(false);

			var addresses = entries.AddressList;
			if (addresses.Length == 0)
			{
				throw new SocketException(11001); // "No such host is known"
			}

			if (addresses.Length == 1)
			{ // only a single IP address, no need to perform any load balancing
				return await ConnectToSingleAsync(clientContext, new IPEndPoint(addresses[0], endpoint.Port), endpoint.Host, ct).ConfigureAwait(false);
			}

			// multiple candidates! we will test them in parallel, using any previous attempt as a hint for ordering
			// - on the first call, we use the order returned by the DNS server, with IPv4 preferred over IPv6
			// - on following calls, we use the "quality" to compute a score (hosts that responded faster before will have a better score, timeouts will have a penalty...)

			var endpoints = new IPEndPoint[addresses.Length];
			var scores = new double[endpoints.Length];
			var now = this.Network.Clock.GetCurrentInstant();
			for(var i = 0; i < endpoints.Length; i++)
			{
				var ep = new IPEndPoint(addresses[i], endpoint.Port);
				endpoints[i] = ep;
				var quality = this.Network.GetEndpointQuality(ep);
				scores[i] = quality?.ComputeScore(now) ?? (ep.AddressFamily == AddressFamily.InterNetworkV6 ? 4d : 2d);
			}
			Array.Sort(scores, endpoints);
#if FULL_DEBUG
			Debug.WriteLine("Will try to connect (in order) to: " + string.Join(", ", endpoints.Select((x, i) => $"{i}) {x} @ {scores[i]}")));
#endif
			return await ConnectToMultipleAsync(clientContext, endpoints, endpoint.Host, ct).ConfigureAwait(false);
		}

		/// <summary>Connects to a remote host using any of the multiple endpoints available.</summary>
		private async ValueTask<Stream> ConnectToMultipleAsync(BetterHttpClientContext? clientContext, IPEndPoint[] endpoints, string? hostName, CancellationToken ct)
		{
			Socket? theOneTrueSocket = null;

			//note: the global "connection timeout" is handled by SocketsHttpHandler, and we will be notified if 'ct' is triggered
			var staggerTimeout = TimeSpan.FromMilliseconds(500);

			var attempts = new (IPEndPoint? Endpoint, TimeSpan Started, Task<Socket>? Task)[endpoints.Length];
			var tasks = new List<Task>();
			ExceptionDispatchInfo? error = null;

			var sw = Stopwatch.StartNew();

			// First we need to "test" the connection
 
			bool aborted = false;

			try
			{
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
				{
					var go = cts.Token;

					int index = 0;

					clientContext?.SetStage(BetterHttpClientStage.Connecting);

					while (theOneTrueSocket == null && !go.IsCancellationRequested)
					{
						if (index < endpoints.Length)
						{ // we can spin another one!
							var ep = endpoints[index];
#if FULL_DEBUG
							Debug.WriteLine($"[{sw.Elapsed.TotalMilliseconds:N1} ms] Attempting connection to {ep}...");
#endif

							var now = sw.Elapsed;
							var attempt = AttemptConnectAsync(ep, go);
							attempts[index] = (ep, now, attempt);
							++index;
						}

						// reset the list of pending tasks
						tasks.Clear();
						Task? timeout = null;
						if (index < endpoints.Length)
						{ // we still have more candidates available...
							timeout = Task.Delay(staggerTimeout, go);
							tasks.Add(timeout);
						}

						foreach (var x in attempts)
						{
							if (x.Task != null)
							{
								tasks.Add(x.Task);
							}
						}

						if (tasks.Count == 0)
						{ // nothing more to do?
							break;
						}

						{ // wait for the next event (timeout or connected socket...)
							var t = await Task.WhenAny(tasks).ConfigureAwait(false);
							if (t == timeout)
							{ // not yet, start another one!
#if FULL_DEBUG
								Debug.WriteLine($"[{sw.Elapsed.TotalMilliseconds:N1} ms] No response yet? maybe start another one!");
#endif
								continue;
							}
						}

						// inspect the results looking for a completed connection...
						for (var i = 0; i < attempts.Length; i++)
						{
							var ts = attempts[i].Task;
							if (ts == null) continue;
							if (ts.IsCompleted)
							{
								attempts[i].Task = null;
								try
								{
									theOneTrueSocket = await ts.ConfigureAwait(false);
									var completed = sw.Elapsed;
									this.Network.RecordEndpointConnectionAttempt(attempts[i].Endpoint!, completed - attempts[i].Started, null, hostName, GetLocalEndpointAddress(theOneTrueSocket));
#if FULL_DEBUG
									Debug.WriteLine($"[{sw.Elapsed.TotalMilliseconds:N1} ms] {attempts[i].Endpoint} connected succesffully after {(completed - attempts[i].Started).TotalMilliseconds:N1} ms !");
#endif
									// exit early
									break;
								}
								catch (Exception e)
								{
									var completed = sw.Elapsed;
									this.Network.RecordEndpointConnectionAttempt(attempts[i].Endpoint!, completed - attempts[i].Started, e, hostName, null);
#if FULL_DEBUG
									Debug.WriteLine($"[{sw.Elapsed.TotalMilliseconds:N1} ms] {attempts[i].Endpoint} has failed after {(completed - attempts[i].Started).TotalMilliseconds:N1} ms: [{e.GetType().Name}] {e.Message}");
#endif
									if (error == null && e is not OperationCanceledException)
									{
										error = ExceptionDispatchInfo.Capture(e);
									}
									// continue!
								}
							}
						}
					}

					await cts.CancelAsync().ConfigureAwait(false);
				}
			}
			catch (Exception e)
			{
				error ??= ExceptionDispatchInfo.Capture(e);
				aborted = true;
			}
			finally
			{
				// if we are cancelled we need to get rid of our socket, even if we have one :(
				aborted |= ct.IsCancellationRequested;

				if (aborted && theOneTrueSocket != null)
				{
					theOneTrueSocket.Dispose();
					theOneTrueSocket = null;
				}

				// if we still have pending tasks, we need to wait for them to complete
				foreach (var attempt in attempts)
				{
					if (attempt.Endpoint == null || attempt.Task == null) continue;

					// all pending tasks should complete since we have killed their cancellation token...
					try
					{
						var socket = await attempt.Task.ConfigureAwait(false);
						var completed = sw.Elapsed;
						// too late! but we will still record it has a success
						this.Network.RecordEndpointConnectionAttempt(attempt.Endpoint, completed - attempt.Started, null, hostName, GetLocalEndpointAddress(socket));
						// get rid of it!
						socket.Dispose();
#if FULL_DEBUG
						Debug.WriteLine($"[{sw.Elapsed.TotalMilliseconds:N1} ms] draining {attempt.Endpoint} started at {attempt.Started.TotalMilliseconds:N1} ms");
#endif
					}
					catch (Exception e)
					{
						var completed = sw.Elapsed;
						this.Network.RecordEndpointConnectionAttempt(attempt.Endpoint, completed - attempt.Started, e, hostName, null);
#if FULL_DEBUG
						Debug.WriteLine($"[{sw.Elapsed.TotalMilliseconds:N1} ms] drained {attempt.Endpoint} started at {attempt.Started.TotalMilliseconds:N1} ms: [{e.GetType().Name}] {e.Message}");
#endif
						if (error == null && e is not OperationCanceledException)
						{
							error = ExceptionDispatchInfo.Capture(e);
						}
					}
				}
			}

			if (aborted || error != null)
			{
				// rethrow first error we got
				error?.Throw();

				// or maybe we got cancelled?
				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
				
				throw new InvalidOperationException("This should not happen!");
			}

			Contract.Debug.Assert(theOneTrueSocket != null);

			clientContext?.Options.Hooks?.OnSocketConnected(clientContext, theOneTrueSocket);

			return new NetworkStream(theOneTrueSocket, ownsSocket: true);
		}

		/// <summary>Wires the multi-IP racing / heatmap connect callback on the underlying sockets.</summary>
		/// <remarks>The callback reads the endpoint heatmap from the <see cref="Network"/> map (map-scoped) and the per-request <see cref="BetterHttpClientContext"/> from <c>request.Options</c>; it stores nothing client-specific on this shared handler.</remarks>
		internal void Setup()
		{
			this.Sockets.ConnectCallback = this.SocketsConnectCallback;
		}

	}

}
