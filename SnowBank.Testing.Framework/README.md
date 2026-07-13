SnowBank.Testing.Framework
==========================

An SDK for writing integration tests for **distributed** .NET applications: spin up several simulated hosts on an in-memory virtual network inside a single test, with all of their traffic routed in-process, and no real sockets, ports, or containers.

# Concept

Derive your fixture from `DistributedTest` and describe a topology with `MakeItSo`: virtual LANs, web hosts, agents and services. Each one is a real host, built the way your production hosts are (DI, minimal APIs, SignalR, ...), but wired onto a **virtual network** instead of real sockets. The framework records everything into a single, ordered **timeline journal** that is printed with the test output, so a failure spread across several hosts reads as one coherent story.

# How to use

```
dotnet add package SnowBank.Testing.Framework
```

```c#
public class MyFacts : DistributedTest
{
    [Test]
    public async Task Two_Hosts_On_A_Virtual_Lan()
    {
        var context = await MakeItSo(env => env.AddSimpleLan(lan =>
        {
            // a simulated web host, configured like a real one
            lan.WithMinimalWebHost("WEB", host =>
            {
                host.ConfigureApplication(app => app.MapGet("/ping", () => "pong"));
            });
        }));

        var web = context.GetWebHost("WEB");

        // `web` is a real host reachable over the virtual network; exercise it from another
        // host, an HTTP client, or a browser (see SnowBank.Testing.Framework.Playwright)
        Assert.That(web.GetUri("/ping"), Is.Not.Null);
    }
}
```

Add a real Chromium to the same network with the companion **`SnowBank.Testing.Framework.Playwright`** package (`lan.WithPlaywrightBrowser(...)`).

> For the timeline-journal format, per-host log levels, always-on HTTP packet capture and the other diagnostics, see the `snowbank-distributed-testing` guidance in the repository.
