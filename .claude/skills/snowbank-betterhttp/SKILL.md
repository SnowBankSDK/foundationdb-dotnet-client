---
name: snowbank-betterhttp
description: How to make outbound HTTP calls with BetterHttpClient (SnowBank.Networking.Http) in an ASP.NET Core or generic-host .NET application - the DI story and the request API. Covers the policy-bundle model (AddBetterHttpClient registers named bundles of wire policy, not origins), IBetterHttpClientFactory and the transient-shell-over-pooled-chain lifetime, the supported doors vs the plain IHttpClientFactory door (which throws for bundle names), the mandatory INetworkMap transport seam (NetworkMap in production, the virtual network inside distributed tests, with packet capture riding every chain automatically), BetterHttpClientOptions (default headers and UserAgent, cookies, credentials, TLS server-certificate callbacks and the self-signed/trusted-roots helpers, filters, hooks, delegating handlers), the Create*Request builders and the SendAsync(request, ctx => ...) lifecycle with BetterHttpClientContext, typed protocols (RestHttpProtocol, BetterHttpProtocolFactoryBase, the AddFooClient extension-method pattern), and how to port legacy HttpWebRequest / WebClient (.NET Framework) code to this stack. Use whenever application code registers or injects an HttpClient in a SnowBank-based app, declares a named client, configures user agents or HTTPS certificate validation, ports HttpWebRequest/.NET Framework 4.7.2 networking code to net8+/net10, wonders why IHttpClientFactory.CreateClient(name) throws InvalidOperationException naming IBetterHttpClientFactory, or hits "You must register an implementation for INetworkMap".
---

# BetterHttpClient - outbound HTTP in an application (DI, options, requests, test interop)

`SnowBank.Networking.Http` replaces ad-hoc `HttpClient` / legacy `HttpWebRequest` usage with a small set of load-bearing pieces. This skill is the consumer guide: wiring the DI, declaring named clients, making requests, and what you get for free inside the distributed test framework. For diagnosing multi-node tests themselves (journal, packet capture output, log knobs) see the **snowbank-distributed-testing** skill.

---

## 1. The mental model: bundles, chains, shells

Three layers, each with its own lifetime:

| Piece | What it is | Lifetime | Who owns it |
|---|---|---|---|
| **Policy bundle** | A NAME registered with `AddBetterHttpClient(name, configure)`: TLS policy, filters, pipeline handlers, default headers. A bundle is **wire policy, not an origin**: the call site provides the absolute target URI at request time. | registration | you (startup code) |
| **Pooled handler chain** | The actual `HttpMessageHandler` stack built per bundle: transport at the bottom, pipeline on top. Owns the sockets. | pooled, managed by `Microsoft.Extensions.Http` | the platform |
| **`BetterHttpClient` shell** | A sealed, thin `HttpClient` subclass over the pooled chain (`disposeHandler: false`). Creating and disposing shells is cheap and **never tears down the shared sockets**. | transient, per use | you (create, use, dispose freely) |

The chain, bottom to top:

```
   [BetterHttpClient shell]            <- transient, IS an HttpClient
        |
   (packet capture, when present)      <- outermost, inserted automatically in tests
   MagicalHandler (pipeline)           <- runs the bundle's filters/hooks per request
   transport wrappers                  <- credentials, custom delegating handlers
   INetworkMap.CreateTransportHandler  <- SocketsHttpHandler in production,
                                          the virtual network inside a DistributedTest
```

Two properties fall out of this design:

- **Late binding**: the target host is resolved per request against the live `INetworkMap`, not captured when the handler is built. A long-lived client keeps working across topology changes (a restarted backend, a re-pointed VIP).
- **No default chain rotation**: bundles are registered with an infinite handler lifetime, because the transport already bounds DNS staleness itself (`PooledConnectionLifetime` on the shared `SocketsHttpHandler`). A bundle that wants periodic chain rebuild opts back in AFTER registration: `services.AddHttpClient(name).SetHandlerLifetime(...)`.

**Why the URI belongs at the call site, not in the registration.** Not just convenience: `HttpClient.BaseAddress` is immutable after the first request, so an address baked in at registration time *cannot* follow a configuration change an admin makes at run time. The pooled transport, by contrast, is origin-agnostic (`SocketsHttpHandler` pools per-origin internally), so a call site passing an absolute URI re-targets for free - the same client starts hitting the new origin's pool and the old connections idle out. Build the absolute URI from live configuration at the moment of the call.

## 2. Startup wiring (both registrations are required)

```csharp
// 1. the transport seam: mandatory. Without it, the first client resolution throws
//    "You must register an implementation for INetworkMap ...".
//    TryAdd, not Add: in production nothing else registers the map (TryAdd == Add), and if this
//    composition ever runs inside a distributed-test host, the framework has ALREADY registered the
//    virtual network map - TryAdd yields to it, a plain Add would clobber the simulation.
services.TryAddSingleton<INetworkMap, NetworkMap>();   // namespace SnowBank.Networking

// 2. the client stack: the defaults hook (routes EVERY factory client through the map, so a plain AddHttpClient
//    needs no enrollment) + the factory singleton
services.AddBetterHttpClientDefaults();

// 3. any named policy bundles the application defines
services.AddBetterHttpClient("Catalog", options =>
{
	options.DefaultRequestHeaders.UserAgent = [ new ProductInfoHeaderValue("AcmeApp", "5.2") ];
	options.AcceptSelfSignedServerCertificates();
});
```

Notes:

- Registering the same name twice **composes** (both configure callbacks run, in order); several call sites can contribute policies to one bundle.
- The name `"SnowBank.Networking.Http.BetterHttpClient"` (`BetterHttpClientExtensions.DefaultClientName`) is reserved for the default bundle.
- Inside a distributed test you do NOT wire `INetworkMap`: the framework registers the virtual network map in every simulated host (see section 8).

## 3. Getting a client: the doors

| Door | API | What you get |
|---|---|---|
| **The normal door** | `IBetterHttpClientFactory.CreateClient()` / `CreateClient(name)` / `CreateClient(baseAddress, name)` / `CreateClient(baseAddress, shell, name)` | a `BetterHttpClient` shell carrying the bundle's filters, hooks and credentials |
| **Bare handler** | `IHttpMessageHandlerFactory.CreateHandler(name)` | the pooled chain itself (packet capture included), for plumbing that builds its own client (gRPC channels, SignalR connections) |
| **The WRONG door** | `IHttpClientFactory.CreateClient(bundleName)` | **throws `InvalidOperationException`** |

The wrong door is guarded on purpose: a plain `HttpClient` over a bundle's chain would have **no BetterHttp runtime** - the bundle's filters, hooks and credentials would silently not run (an auth-signing bundle would silently not sign). The registration fails that door loudly instead. `IHttpClientFactory.CreateClient` keeps working normally for names that are not policy bundles.

`BetterHttpClient` **is** an `HttpClient` (sealed subclass), so a service can depend on plain `HttpClient` while receiving a fully configured instance - as long as the instance is created through the factory:

```csharp
// a service that wants an HttpClient injected directly:
services.AddSingleton<CatalogGateway>(sp =>
	new CatalogGateway(sp.GetRequiredService<IBetterHttpClientFactory>()
		.CreateClient(new Uri("https://catalog.acme.local"), "Catalog")));
```

Holding one shell long-lived is correct (late binding keeps routing it against the live network), but the idiomatic pattern is to inject `IBetterHttpClientFactory` and create a shell per operation: creation is cheap and `Dispose` never closes sockets.

## 4. Making requests

Add `using SnowBank.Networking.Http;` - the request API lives in extension methods on `HttpClient`:

- Request builders: `client.CreateGetRequest(path)`, `CreatePostRequest(path, content)`, `CreatePutRequest`, `CreatePatchRequest`, `CreateDeleteRequest`, `CreateHeadRequest`, `CreateOptionsRequest`, `CreateTraceRequest` (each with `string` or `Uri` overloads, resolved against `BaseAddress`).
- The send lifecycle: `client.SendAsync(request, handler, ct)` where the handler receives a **`BetterHttpClientContext`** while the response is still open:

```csharp
var result = await client.SendAsync(
	client.CreateGetRequest("/api/catalog"),
	async (ctx) =>
	{
		ctx.EnsureSuccessStatusCode();
		return await ctx.ReadAsJsonAsync<CatalogDto>();   // CrystalJson deserialization
	},
	ct);
```

`BetterHttpClientContext` carries `Request`, `Response`, the DI `Services`, the injected `Clock`, a per-request `State` bag (how filters coordinate their stages), and helpers: `EnsureSuccessStatusCode()`, `ReadAsJsonAsync()` / `ReadAsJsonObjectAsync()` / `ReadAsJsonArrayAsync()` / `ReadAsJsonAsync<TResult>()`.

Cancellation tokens are required across this stack, never optional: pass the caller's real token (`HttpContext.RequestAborted`, a `BackgroundService`'s `stoppingToken`, ...).

> **Parity note:** the stage hooks belong to `SendAsync(request, handler, ct)`. Talking to the transport directly - a bare `HttpClient.GetAsync(...)` on the shell - bypasses them; only in-chain handlers fire. The stack also owns disposal of the request/response around your callback, which is why the callback processes the response *while it is still open* rather than returning it.

## 5. Options: three scopes, one rule

| Scope | Type | Where | What belongs there |
|---|---|---|---|
| **Bundle** (wire policy) | `BetterHttpClientOptions` | `AddBetterHttpClient(name, options => ...)` at startup | TLS/certificates, proxy, cookies, credentials, filters, hooks, delegating handlers, default headers |
| **Shell** (one client) | `BetterHttpShellOptions` | `factory.CreateClient(baseAddress, shell, name)` | default headers, request version, hooks for THIS client instance only |
| **Per call** (typed protocols) | the protocol's options | `protocolFactory.CreateClient(uri, o => ...)` | protocol/client behavior only |

The rule: **wire policy lives on the bundle, at startup**. A per-call configure that touches wire policy (a TLS callback, a filter, a pipeline handler) cannot reach the shared pooled transport, and silently ignoring it would be a silent security break - so it **throws**, naming the offending member.

Useful `BetterHttpClientOptions` members:

- `DefaultRequestHeaders` (a `BetterDefaultHeaders`; includes `UserAgent`), `Cookies`, `Credentials`, `DefaultProxyCredentials`.
- `Filters` (`IBetterHttpFilter`, staged per request), `Hooks` (`IBetterHttpHooks`), `Handlers` + `WithDelegatingHandler<THandler>()` for classic `DelegatingHandler`s.
- TLS: `ServerCertificateCustomValidationCallback` (connection-shaped: `(cert, chain, errors) => bool`, no request argument - it validates a connection, and maps directly onto the socket transport's `SslOptions`), `ClientCertificates`, `ClientCertificateOptions`, `CheckCertificateRevocationList`.
- TLS helpers, in decreasing order of preference:
  - `TrustServerCertificates(params X509Certificate2[] roots)` - pin known roots;
  - `AcceptSelfSignedServerCertificates()` - accept an otherwise-valid self-signed leaf (typical for appliances);
  - `DangerousAcceptAnyServerCertificate()` - accept everything; test/lab only, the name is the warning.

Process-wide policies: `services.AddGlobalHttpFilter<TFilter>()` and `services.AddGlobalHttpHandler(factory)` apply to every bundle in the process.

## 6. Porting legacy HttpWebRequest code (the AddFooClient recipe)

Where legacy .NET Framework members go:

| Legacy (`HttpWebRequest` / `ServicePointManager`) | Now |
|---|---|
| `WebRequest.Create(url)` per call | one injected factory/shell; absolute URI per request |
| `req.UserAgent = "AcmeApp/5.2"` | bundle `options.DefaultRequestHeaders.UserAgent` |
| `req.ServerCertificateValidationCallback = ... => true` | bundle `AcceptSelfSignedServerCertificates()` (or `TrustServerCertificates`; reserve `DangerousAcceptAnyServerCertificate` for tests) |
| `req.Headers.Add("Authorization", ...)` | per request (`request.Headers.Authorization = ...`), or an auth filter on the bundle |
| `req.GetResponse()` + `StreamReader` (sync) | `await client.SendAsync(request, ctx => ..., ct)` |
| `req.Proxy`, `req.Credentials`, `req.CookieContainer` | bundle `DefaultProxyCredentials` / `Credentials` / `Cookies` |
| `ServicePointManager` global state | per-bundle options (there is no process-global mutable state) |

Before (net472):

```csharp
public class CatalogGateway
{
	public string FetchCatalog(string server, string token)
	{
		var req = (HttpWebRequest) WebRequest.Create($"https://{server}/api/catalog");
		req.UserAgent = "AcmeApp/5.2";
		req.Headers.Add("Authorization", "Bearer " + token);
		req.ServerCertificateValidationCallback = (s, cert, chain, errors) => true;
		using var resp = (HttpWebResponse) req.GetResponse();
		using var reader = new StreamReader(resp.GetResponseStream());
		return reader.ReadToEnd();
	}
}
```

After (net8+/net10), service plus its registration extension:

```csharp
public sealed class CatalogGateway
{
	public CatalogGateway(IBetterHttpClientFactory clients)
	{
		this.Clients = clients;
	}

	private IBetterHttpClientFactory Clients { get; }

	public async Task<string> FetchCatalogAsync(Uri server, string token, CancellationToken ct)
	{
		using var client = this.Clients.CreateClient(server, "Catalog");
		var request = client.CreateGetRequest("/api/catalog");
		request.Headers.Authorization = new("Bearer", token);
		return await client.SendAsync(request, async (ctx) =>
		{
			ctx.EnsureSuccessStatusCode();
			return await ctx.Response.Content.ReadAsStringAsync(ct);
		}, ct);
	}
}

public static class CatalogClientExtensions
{
	/// <summary>Registers the Catalog gateway and its HTTP policy bundle.</summary>
	public static IServiceCollection AddCatalogClient(this IServiceCollection services, Action<BetterHttpClientOptions>? configure = null)
	{
		services.AddBetterHttpClient("Catalog", options =>
		{
			options.DefaultRequestHeaders.UserAgent = [ new ProductInfoHeaderValue("AcmeApp", "5.2") ];
			options.AcceptSelfSignedServerCertificates();   // self-signed appliances
			configure?.Invoke(options);                      // let the host override defaults
		});
		services.AddSingleton<CatalogGateway>();
		return services;
	}
}
```

The host wires `AddCatalogClient()` once; the gateway never constructs an `HttpClient` and never touches certificates - both are bundle policy, overridable by the host through the `configure` hook.

## 7. Typed protocols (`IBetterHttpProtocol`)

For a structured API surface (instead of raw requests), wrap the client in a protocol:

- **`RestHttpProtocol`** ships in this repo: `services.AddRestHttpProtocol(options => ...)`, then inject `RestHttpProtocolFactory` and `factory.CreateClient(baseAddress, o => ...)` for a JSON/REST convenience client.
- Consuming SDKs layer their own (e.g. a JSON-API protocol in an application-level SDK).
- To build one: implement `IBetterHttpProtocol` + a factory deriving `BetterHttpProtocolFactoryBase<TProtocol, TOptions>` (with `TOptions : BetterHttpClientOptions`), and expose it as a dedicated `AddFooProtocol()` extension that calls `services.AddBetterHttpProtocol<TFactory, TProtocol, TOptions>(configure)`.

Remember the scope rule from section 5: the per-call `configure` on `factory.CreateClient(uri, o => ...)` is for protocol behavior; wire policy there throws.

## 8. Inside the distributed test framework: zero-change interop

When application code runs on a simulated host of a `DistributedTest` (see the **snowbank-distributed-testing** skill), the SAME registrations acquire test behavior automatically - do not add any test-specific wiring to application code:

- **The virtual network replaces the transport.** The framework registers the virtual network map as the host's `INetworkMap`, so every bundle's chain bottoms out in the simulated network. In-test web hosts are reachable by their simulated names; TestServer-style in-memory handlers, simulated latency and faults all live below the same seam.
- **Packet capture rides every chain.** The capture handler is inserted as the outermost handler of every pooled bundle (bare handlers included, so gRPC and SignalR traffic shows up like any other request): every test journal carries one `H` line per request, with bodies dumped on failure. No registration needed. Only the deliberately-raw transport (`INetworkMap.CreateTransportHandler`) stays uncaptured. A **long-lived streaming response** - `application/grpc*` or `text/event-stream` - is captured at **headers only** (metadata plus a `Streaming` flag; the body is never mirrored, so the stream is never torn); finite bodies capture exactly.
- **Failures keep their historical shapes.** An unresolvable name surfaces as `HttpRequestException` wrapping a `WebException` with `WebExceptionStatus.NameResolutionFailure`; a stopped host fails like a connect failure. Ported legacy code that matches on these shapes keeps working.
- **Late binding is observable.** Stopping, restarting or re-aliasing a host reroutes the SAME live client on its next request - tests can exercise reconnect logic without recreating clients.

The one thing to avoid in application code: constructing `new HttpClient(new SocketsHttpHandler())` (or `new HttpClient()`) directly. That bypasses `INetworkMap`, so in a distributed test it tries to hit the real network and defeats the simulation (and in production it forfeits the bundle policies and capture seam).

## 9. Common mistakes

| Mistake | What happens / the fix |
|---|---|
| `IHttpClientFactory.CreateClient("MyBundle")` | throws `InvalidOperationException` naming `IBetterHttpClientFactory`; use the factory (shells) or `IHttpMessageHandlerFactory.CreateHandler` (bare handler) |
| No `INetworkMap` registered | first resolution throws "You must register an implementation for INetworkMap"; add `services.TryAddSingleton<INetworkMap, NetworkMap>()` in the host composition root |
| Plain `AddSingleton<INetworkMap, NetworkMap>()` in wiring reused by tests | overrides the test framework's virtual map (user registrations run after the framework's base wiring and win), so the "simulated" host silently talks to the real network; use `TryAddSingleton` |
| Treating a bundle name as an origin | bundles carry policy, not addresses: pass the target `Uri` at `CreateClient`/request time |
| TLS callback or filter in a per-call protocol configure | throws by design; move it to the bundle registration |
| Caching `HttpClient` instances "because sockets" | unnecessary: shells are cheap and disposing them never closes the pooled sockets; create per use |
| `new HttpClient(...)` in application code | bypasses the map, the bundle policies, and packet capture; breaks under the test framework |
| Expecting chain rotation | bundles default to an infinite handler lifetime (the transport bounds DNS staleness); opt in with `services.AddHttpClient(name).SetHandlerLifetime(...)` AFTER the bundle registration |
| Fixture/DI hangs on `Task.Result` of a send | the whole stack is async with required tokens; port sync `GetResponse()` call sites to async all the way |
