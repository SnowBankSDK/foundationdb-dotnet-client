---
name: snowbank-betterhttp
description: How to make outbound HTTP calls with BetterHttpClient (SnowBank.Networking.Http) in an ASP.NET Core or generic-host .NET application - the DI story and the request API. Covers the one-policy-plane model where a client NAME maps to one effective policy that lives in the pooled handler chain, so every client gets the same behavior: a typed client, a keyed client, IHttpClientFactory.CreateClient(name), and the bare IHttpMessageHandlerFactory.CreateHandler(name). Covers AddBetterHttpClientDefaults (routes every factory client through the map), AddBetterHttpClient(name, ...) returning an IBetterHttpClientBuilder for standard chaining (AddHttpMessageHandler, AddAsKeyed, typed clients), the AddBetterHttpClientConfiguration override layer bound from an appsettings section, the mandatory INetworkMap transport seam (NetworkMap in production, the virtual network inside distributed tests, with packet capture on every chain automatically), BetterHttpClientOptions (default headers and UserAgent, cookies, credentials, TLS server-certificate callbacks and the self-signed/trusted-roots helpers, hooks, delegating handlers), the universal SendAsync(request, ctx => ...) lifecycle with BetterHttpClientContext that now runs the request stage on any client, the Create*Request builders, typed protocols (RestHttpProtocol, BetterHttpProtocolFactoryBase, the AddFooClient extension-method pattern), how to port legacy HttpWebRequest / WebClient (.NET Framework) code to this stack, and which 7.4.3 members are now soft-obsolete (IBetterHttpClientFactory, the BetterHttpClient shell, BetterHttpShellOptions, IBetterHttpFilter/Filters, AddGlobalHttpFilter/AddGlobalHttpHandler) and their native replacements. Use whenever application code registers or injects an HttpClient in a SnowBank-based app, declares a named client, configures user agents or HTTPS certificate validation, binds HTTP options from configuration, ports HttpWebRequest/.NET Framework 4.7.2 networking code to net8+/net10, or hits "You must register an implementation for INetworkMap".
---

<!-- Publisher note: the frontmatter description is skill-optimizer output. It was hand-corrected on the 7.4.4 rewrite to drop the now-false claims (the IHttpClientFactory throw on named clients, the shell-as-primary lifetime). Re-run the skill-creator optimizer before shipping, do not hand-tune further. -->

# BetterHttpClient - outbound HTTP in an application (DI, options, requests, test interop)

`SnowBank.Networking.Http` replaces ad-hoc `HttpClient` / legacy `HttpWebRequest` usage with a small set of load-bearing pieces. This skill is the consumer guide: wiring the DI, declaring named clients, making requests, and what the distributed test framework adds automatically. For diagnosing multi-node tests themselves (journal, packet capture output, log knobs) see the **snowbank-distributed-testing** skill.

> **7.4.4 changed the model.** A client name now maps to one effective policy that lives in the pooled handler chain, so every client behaves the same, including a plain injected `HttpClient`. The old `IBetterHttpClientFactory`, the `BetterHttpClient` wrapper, `BetterHttpShellOptions`, `IBetterHttpFilter`, and the global-filter helpers still work but are `[Obsolete]` warnings now. Section 10 maps each retired member to its replacement.

---

## 1. The mental model: one policy per name, on the chain

Three layers, each with its own lifetime:

| Piece | What it is | Lifetime | Who owns it |
|---|---|---|---|
| **Named policy** | A name registered with `AddBetterHttpClient(name, configure)`: TLS policy, credentials, default headers, hooks, delegating handlers. A name is **client policy, not an origin**: the call site provides the absolute target URI at request time. | registration | you (startup code) |
| **Pooled handler chain** | The actual `HttpMessageHandler` stack built per name: transport at the bottom, the pipeline handler and any application handlers on top. Owns the sockets. | pooled, managed by `Microsoft.Extensions.Http` | the platform |
| **`HttpClient`** | A plain client the factory hands you: a typed client, a keyed client, or one from `IHttpClientFactory.CreateClient(name)`. Cheap to create, and disposing it never tears down the shared sockets. | transient, per use | you |

The chain, bottom to top:

```
   [HttpClient, however obtained]       <- typed, keyed, or IHttpClientFactory.CreateClient(name)
        |
   (packet capture, when present)       <- outermost, inserted automatically in tests
   application handlers                  <- AddHttpMessageHandler, in registration order
   BetterHttpPipelineHandler             <- runs the name's request stage per request
   transport wrappers                    <- credentials' transport half, custom delegating handlers
   INetworkMap.CreateTransportHandler    <- SocketsHttpHandler in production,
                                            the virtual network inside a DistributedTest
```

`BetterHttpPipelineHandler` (the piece that replaced `MagicalHandler`) is the difference from 7.4.3. On a plain `client.GetAsync(...)` it builds the `BetterHttpClientContext` itself and runs the name's request stage (credentials, hooks, default headers), so **the full policy runs for every client, not only on `SendAsync` calls through the retired `BetterHttpClient` wrapper**. A request that already carries a context (from the `SendAsync(request, ctx => ...)` extension) is enriched and runs the same lifecycle.

This design gives two properties:

- **Late binding**: the target host is resolved per request against the live `INetworkMap`, not captured when the handler is built. A long-lived client keeps working across topology changes (a restarted backend, a re-pointed VIP).
- **No default chain rotation**: names are registered with an infinite handler lifetime, because the transport already bounds DNS staleness itself (`PooledConnectionLifetime` on the shared `SocketsHttpHandler`). To rebuild the chain periodically for a name, opt back in after registration: `services.AddHttpClient(name).SetHandlerLifetime(...)`.

**Why the URI belongs at the call site, not in the registration.** `HttpClient.BaseAddress` is immutable after the first request, so an address baked in at registration time *cannot* follow a configuration change an admin makes at run time. The pooled transport, by contrast, is origin-agnostic (`SocketsHttpHandler` pools per-origin internally), so a call site that passes an absolute URI re-targets: the same client starts hitting the new origin's pool and the old connections idle out. Build the absolute URI from live configuration at the moment of the call.

## 2. Startup wiring

The public startup API is three methods; the transport seam is mandatory.

```csharp
// 1. the transport seam: mandatory. Without it, the first client resolution throws
//    "You must register an implementation for INetworkMap ...".
//    TryAdd, not Add: in production nothing else registers the map (TryAdd == Add), and if this
//    composition ever runs inside a distributed-test host, the framework has ALREADY registered the
//    virtual network map - TryAdd yields to it, a plain Add would clobber the simulation.
services.TryAddSingleton<INetworkMap, NetworkMap>();   // namespace SnowBank.Networking

// 2. the defaults hook: routes EVERY factory client through the map, so a plain AddHttpClient
//    needs no enrollment. The configure sets the baseline for every client.
services.AddBetterHttpClientDefaults(options =>
{
    options.DefaultRequestHeaders.UserAgent = [ new ProductInfoHeaderValue("AcmeApp", "5.2") ];
});

// 3. any named clients that need their own policy (certificates, credentials, handlers)
services.AddBetterHttpClient("Catalog", options =>
{
    options.AcceptSelfSignedServerCertificates();
});
```

Notes:

- `AddBetterHttpClientDefaults(configure)` is **the one mandatory call**. It installs a `ConfigureHttpClientDefaults` hook that routes every factory client (named, typed via `AddHttpClient<TClient>`, keyed, or a plain `AddHttpClient("x")`) through the map, so a stock client needs no enrollment and a distributed-test host sandboxes every factory client by construction. The global `configure` sets the baseline (transport, default headers, TLS trust, credentials) for every client.
- `AddBetterHttpClient("name", configure)` adds a named client whose per-client options override that baseline. A client with **no** BetterHttp-specific policy does not need it: a plain `services.AddHttpClient("weather", c => c.Timeout = ...)` is already fully enrolled by the defaults hook.
- Both are safe to call more than once: each `configure` composes (in order), and the defaults hook installs once.
- Registering the same name twice composes: both configure callbacks run, so several call sites can contribute policies to one name.
- The name `"SnowBank.Networking.Http.BetterHttpClient"` (`BetterHttpClientExtensions.DefaultClientName`) is reserved for the default client.
- Inside a distributed test you do not wire `INetworkMap`: the framework registers the virtual network map in every simulated host (see section 9).

**`AddBetterHttpClient(name, ...)` returns an `IBetterHttpClientBuilder`.** It derives from the native `IHttpClientBuilder`, so the standard registration extensions chain on, and BetterHttp-specific extensions target it:

```csharp
services.AddBetterHttpClient("Catalog", options => options.AcceptSelfSignedServerCertificates())
    .AddHttpMessageHandler<RetryHandler>()   // an application DelegatingHandler
    .AddAsKeyed();                            // keyed injection, Microsoft.Extensions.Http 9.0+
```

The old no-name `AddBetterHttpClient(configure)` overload stays retired (`[Obsolete(error: true)]`): it wired only the default client, so a stock `AddHttpClient` escaped the map. Call `AddBetterHttpClientDefaults(configure)`.

## 3. Getting a client: every kind is equivalent

Every kind of client gives the same policy, because the policy lives in the chain. Pick by consumer lifetime:

| Consumer | Client kind | API |
|---|---|---|
| Request-scoped (controllers, per-request services) | typed or keyed client | `AddHttpClient<CatalogService>()` (ctor-injected `HttpClient`), or `.AddAsKeyed()` then `[FromKeyedServices("Catalog")] HttpClient` |
| Singletons, static-cached factories | `IHttpClientFactory` | `factory.CreateClient("Catalog")`, `using var` per operation |
| Third-party libs that build their own client (gRPC, SignalR, Kiota) | `IHttpMessageHandlerFactory` | `factory.CreateHandler("Catalog")` returns the bare pooled chain |

All four carry the name's full policy, including packet capture inside tests. A plain `HttpClient` is enough: a service can depend on `HttpClient` (typed client) and receive a fully configured instance.

```csharp
// a singleton that talks to Catalog:
public sealed class CatalogGateway
{
    public CatalogGateway(IHttpClientFactory clients) => this.Clients = clients;
    private IHttpClientFactory Clients { get; }

    public async Task<CatalogDto> FetchAsync(Uri origin, CancellationToken ct)
    {
        using var client = this.Clients.CreateClient("Catalog");
        var request = client.CreateGetRequest(new Uri(origin, "/api/catalog"));
        return await client.SendAsync(request, async ctx =>
        {
            ctx.EnsureSuccessStatusCode();
            return await ctx.ReadAsJsonAsync<CatalogDto>();
        }, ct);
    }
}
```

Holding one client long-lived is correct (late binding keeps routing it against the live network), but the per-operation `CreateClient` idiom stays the convention for long-lived services: creation is cheap and `Dispose` never closes sockets.

> **Legacy:** `IBetterHttpClientFactory.CreateClient(...)` and the `BetterHttpClient` wrapper still resolve, now under `[Obsolete]` warnings. They are no longer the primary way to get a client. Section 10 lists the replacements.

## 4. Making requests

Add `using SnowBank.Networking.Http;` - the request API lives in extension methods on `HttpClient`, so it works on any client:

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

`BetterHttpClientContext` carries `Request`, `Response`, the DI `Services`, the injected `Clock`, a per-request `State` bag (how stages coordinate), and helpers: `EnsureSuccessStatusCode()`, `ReadAsJsonAsync()` / `ReadAsJsonObjectAsync()` / `ReadAsJsonArrayAsync()` / `ReadAsJsonAsync<TResult>()`.

Cancellation tokens are required across this stack, never optional: pass the caller's real token (`HttpContext.RequestAborted`, a `BackgroundService`'s `stoppingToken`, ...).

> **What a plain `GetAsync` gets.** Since 7.4.4 the in-chain `BetterHttpPipelineHandler` runs the name's request stage (credentials, hooks, default headers) even for a bare `client.GetAsync(...)`. So a signing credential on the name signs a plain `GetAsync` too. The `SendAsync(request, ctx => ...)` form adds the context callback, the `State` bag, and the JSON helpers, and lets you read the response while the stack still owns disposal, which is why the callback processes the response rather than returning it.

## 5. Options and scopes

| Scope | Type | Where | What belongs there |
|---|---|---|---|
| **Per name** (client policy) | `BetterHttpClientOptions` | `AddBetterHttpClient(name, options => ...)` at startup | TLS/certificates, proxy, cookies, credentials, hooks, delegating handlers, default headers |
| **Configuration override** | the `BetterHttp` section | `AddBetterHttpClientConfiguration(configuration)` | the ops-safe subset (section 6), applied after code, last word |
| **Per call** (typed protocols) | the protocol's options | `protocolFactory.CreateClient(uri, o => ...)` | protocol/client behavior only |

The rule: **client policy lives on the name, at startup**. A per-call configure that touches client policy (a TLS callback, a delegating handler) cannot reach the shared pooled transport, and ignoring it would be a silent security break, so it **throws**, naming the offending member. The per-call side may set client behavior only: default headers, request options, hooks, `Timeout`, and per-request-only credentials (a message signer stamping a different identity per client); all of them run per request, in the chain.

Useful `BetterHttpClientOptions` members:

- `DefaultRequestHeaders` (a `BetterDefaultHeaders`; includes `UserAgent`), `Cookies`, `Credentials`, `DefaultProxyCredentials`.
- `Hooks` (`IBetterHttpHooks`), `Handlers` and `WithDelegatingHandler<THandler>()` for classic `DelegatingHandler`s. For per-request stages, prefer a standard `DelegatingHandler` added with `.AddHttpMessageHandler<T>()` on the builder.
- TLS: `ServerCertificateCustomValidationCallback` (connection-shaped: `(cert, chain, errors) => bool`, no request argument, it validates a connection and maps directly onto the socket transport's `SslOptions`), `ClientCertificates`, `ClientCertificateOptions`, `CheckCertificateRevocationList`.
- TLS helpers, in decreasing order of preference:
  - `TrustServerCertificates(params X509Certificate2[] roots)` - pin known roots;
  - `AcceptSelfSignedServerCertificates()` - accept an otherwise-valid self-signed leaf (typical for appliances);
  - `DangerousAcceptAnyServerCertificate()` - accept everything; test/lab only, the name is the warning, and it is `[Obsolete]` so the call site must acknowledge it with a `#pragma`.

> **Retired scope:** the `BetterHttpShellOptions` override (passed to the old `factory.CreateClient(baseAddress, shell, name)`) is gone. Put policy on the name, or set it on the request itself.

## 6. Binding options from configuration

`AddBetterHttpClientConfiguration(configuration, sectionName = "BetterHttp")` registers a configuration override layer. The section is a **pure override**: when it is absent, the code-configured behavior runs unchanged.

```csharp
services.AddBetterHttpClientConfiguration(builder.Configuration);   // reads the "BetterHttp" section
```

```json
"BetterHttp": {
  "Defaults": {
    "AutomaticDecompression": "All"
  },
  "Clients": {
    "Catalog": {
      "Timeout": "00:00:30",
      "Tls": { "Mode": "AcceptSelfSigned" }
    }
  }
}
```

- `Defaults` overrides the global baseline for every client; `Clients:<name>` overrides one named client. Both apply after the code layers, so configuration has the last word.
- Only the operation-safe subset binds: `Timeout`, `AllowAutoRedirect`, `AutomaticDecompression`, and `Tls:Mode` (`System`, `AcceptSelfSigned`, `AcceptAny`). Credentials, hooks, handlers, and TLS callbacks are code-only by construction, they cannot be reached from a string.
- A knob can carry `"inherit"` to cancel every override below the global layers, so the effective value falls back to the code-global baseline (for a knob in `Clients:<name>`, this also cancels that client's own code configure).
- Repeated calls compose, applied in registration order.

## 7. Porting legacy HttpWebRequest code (the AddFooClient recipe)

Where legacy .NET Framework members go:

| Legacy (`HttpWebRequest` / `ServicePointManager`) | Now |
|---|---|
| `WebRequest.Create(url)` per call | one injected client; absolute URI per request |
| `req.UserAgent = "AcmeApp/5.2"` | `options.DefaultRequestHeaders.UserAgent` on the name |
| `req.ServerCertificateValidationCallback = ... => true` | `AcceptSelfSignedServerCertificates()` (or `TrustServerCertificates`; reserve `DangerousAcceptAnyServerCertificate` for tests) |
| `req.Headers.Add("Authorization", ...)` | per request (`request.Headers.Authorization = ...`), or a credential on the name |
| `req.GetResponse()` + `StreamReader` (sync) | `await client.SendAsync(request, ctx => ..., ct)` |
| `req.Proxy`, `req.Credentials`, `req.CookieContainer` | `DefaultProxyCredentials` / `Credentials` / `Cookies` on the name |
| `ServicePointManager` global state | per-client options (there is no process-global mutable state) |

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

After (net8+/net10), a typed client plus its registration extension:

```csharp
public sealed class CatalogGateway
{
    public CatalogGateway(HttpClient client) => this.Client = client;

    private HttpClient Client { get; }

    public async Task<string> FetchCatalogAsync(Uri server, string token, CancellationToken ct)
    {
        var request = this.Client.CreateGetRequest(new Uri(server, "/api/catalog"));
        request.Headers.Authorization = new("Bearer", token);
        return await this.Client.SendAsync(request, async (ctx) =>
        {
            ctx.EnsureSuccessStatusCode();
            return await ctx.Response.Content.ReadAsStringAsync(ct);
        }, ct);
    }
}

public static class CatalogClientExtensions
{
    /// <summary>Registers the Catalog gateway and its HTTP policy.</summary>
    public static IServiceCollection AddCatalogClient(this IServiceCollection services, Action<BetterHttpClientOptions>? configure = null)
    {
        services.AddBetterHttpClient("Catalog", options =>
        {
            options.DefaultRequestHeaders.UserAgent = [ new ProductInfoHeaderValue("AcmeApp", "5.2") ];
            options.AcceptSelfSignedServerCertificates();   // self-signed appliances
            configure?.Invoke(options);                      // let the host override defaults
        });
        services.AddHttpClient<CatalogGateway>("Catalog");   // typed client on the named policy
        return services;
    }
}
```

The host wires `AddCatalogClient()` once; the gateway takes a plain `HttpClient` and never touches certificates. The `AddHttpClient<CatalogGateway>("Catalog")` typed-client registration binds the gateway to the "Catalog" policy.

## 8. Typed protocols (`IBetterHttpProtocol`)

For a structured API (instead of raw requests), wrap the client in a protocol:

- **`RestHttpProtocol`** ships in this repo: `services.AddRestHttpProtocol(options => ...)`, then inject `RestHttpProtocolFactory` and `factory.CreateClient(baseAddress, o => ...)` for a JSON/REST convenience client.
- Consuming SDKs layer their own (e.g. a JSON-API protocol in an application-level SDK).
- To build one: implement `IBetterHttpProtocol` + a factory deriving `BetterHttpProtocolFactoryBase<TProtocol, TOptions>` (with `TOptions : BetterHttpClientOptions`), and expose it as a dedicated `AddFooProtocol()` extension that calls `services.AddBetterHttpProtocol<TFactory, TProtocol, TOptions>(configure)`.

Remember the scope rule from section 5: the per-call `configure` on `factory.CreateClient(uri, o => ...)` is for protocol/client behavior (headers, request options, hooks, `Timeout`, per-request-only credentials); client policy there throws.

## 9. Inside the distributed test framework: zero-change interop

When application code runs on a simulated host of a `DistributedTest` (see the **snowbank-distributed-testing** skill), the same registrations acquire test behavior automatically. Do not add any test-specific wiring to application code:

- **The virtual network replaces the transport.** The framework registers the virtual network map as the host's `INetworkMap`, so every name's chain bottoms out in the simulated network. In-test web hosts are reachable by their simulated names; TestServer-style in-memory handlers, simulated latency and faults all live below the same seam.
- **Packet capture is on every chain.** The capture handler is inserted as the outermost handler of every pooled chain (bare handlers included, so gRPC and SignalR traffic shows up like any other request): every test journal carries one `H` line per request, with bodies dumped on failure. No registration needed. Only the deliberately-raw transport (`INetworkMap.CreateTransportHandler`) stays uncaptured. A **long-lived streaming response** (`application/grpc*` or `text/event-stream`) is captured at **headers only** (metadata plus a `Streaming` flag; the body is never mirrored, so the stream is never torn); finite bodies capture exactly.
- **Request-stage policy runs in tests too.** Because the pipeline handler is in-chain, credentials (signing), hooks, and default headers now run identically inside a distributed test for a plainly-injected client, not only for the retired `BetterHttpClient` wrapper.
- **Failures keep their historical shapes.** An unresolvable name surfaces as `HttpRequestException` wrapping a `WebException` with `WebExceptionStatus.NameResolutionFailure`; a stopped host fails like a connect failure. Ported legacy code that matches on these shapes keeps working.
- **Late binding is observable.** Stopping, restarting or re-aliasing a host reroutes the same live client on its next request, so tests can exercise reconnect logic without recreating clients.

The one thing to avoid in application code: constructing `new HttpClient(new SocketsHttpHandler())` (or `new HttpClient()`) directly. That bypasses `INetworkMap`, so in a distributed test it tries to hit the real network and defeats the simulation (and in production it forfeits the name's policy and the capture seam).

## 10. Migrating off the 7.4.3 API

These members still work in 7.4.4 under `[Obsolete]` warnings. Move to the replacement when you touch the call site.

| Soft-obsolete (7.4.4) | Replacement |
|---|---|
| `IBetterHttpClientFactory`, `DefaultBetterHttpClientFactory` | inject `IHttpClientFactory` (or a typed/keyed client) and use the `SendAsync` extensions on the `HttpClient` |
| `BetterHttpClient` (the retired wrapper type) | any `HttpClient`; the `SendAsync` extensions work on all of them |
| `BetterHttpShellOptions` (the per-instance override) | per-client options on `AddBetterHttpClient`; for a per-call client tier (headers, hooks, timeout, per-request-only credentials), `services.CreateBetterHttpClient(uri, configure, name)` |
| `IBetterHttpFilter`, `BetterHttpClientOptions.Filters` | a standard `DelegatingHandler` via `.AddHttpMessageHandler<T>()`, or `IBetterHttpHooks` for pure observation |
| `AddGlobalHttpFilter<T>()` | `ConfigureHttpClientDefaults(b => b.AddHttpMessageHandler<T>())` |
| `AddGlobalHttpHandler(factory)` | `ConfigureHttpClientDefaults(b => b.AddHttpMessageHandler(...))` |

Kept and unchanged: `BetterHttpClientOptions` and its TLS helpers, `IBetterCredentials`, `IBetterHttpHooks`, `BetterHttpClientContext`, the `Create*Request` builders and the `SendAsync` extensions, `INetworkMap` / `NetworkMap`, the capture seam, and the typed protocols (`RestHttpProtocol`, `BetterHttpProtocolFactoryBase`), rebuilt internally but with their public `CreateClient(uri, configure)` API intact.

## 11. Common mistakes

| Mistake | What happens / the fix |
|---|---|
| No `INetworkMap` registered | first resolution throws "You must register an implementation for INetworkMap"; add `services.TryAddSingleton<INetworkMap, NetworkMap>()` in the host composition root |
| Plain `AddSingleton<INetworkMap, NetworkMap>()` in wiring reused by tests | overrides the test framework's virtual map (user registrations run after the framework's base wiring and win), so the "simulated" host silently talks to the real network; use `TryAddSingleton` |
| Treating a name as an origin | names carry policy, not addresses: pass the target `Uri` at `CreateClient`/request time |
| TLS callback or handler in a per-call protocol configure | throws by design; move it to the name registration |
| Caching `HttpClient` instances "because sockets" | unnecessary: clients are cheap and disposing them never closes the pooled sockets; create per use |
| `new HttpClient(...)` in application code | bypasses the map, the name's policy, and packet capture; breaks under the test framework |
| Expecting chain rotation | names default to an infinite handler lifetime (the transport bounds DNS staleness); opt in with `services.AddHttpClient(name).SetHandlerLifetime(...)` after the name registration |
| Fixture/DI hangs on `Task.Result` of a send | the whole stack is async with required tokens; port sync `GetResponse()` call sites to async all the way |
| Reaching for `IBetterHttpFilter` or `AddGlobalHttpFilter` | soft-obsolete; use a `DelegatingHandler` with `.AddHttpMessageHandler<T>()` or `ConfigureHttpClientDefaults` (section 10) |
