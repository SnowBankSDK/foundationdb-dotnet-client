SnowBank.Networking.PacketCapture
=================================

Captures HTTP request and response traffic for diagnostics and testing of distributed .NET applications.

# Concept

Register the capture in your pipeline and every HTTP exchange (headers, timing, and within limits bodies) is recorded to an `IPacketCaptureManager`, which you can then read back from a test or a diagnostics endpoint.

It is designed for **short-lived** capture (a test, a repro, a targeted investigation), not long-running production tracing: captured data is held in memory for the duration of the run.

# How to use

```
dotnet add package SnowBank.Networking.PacketCapture
```

On an ASP.NET Core host, register the services and add the middleware:

```c#
builder.Services.AddPacketCapture();

var app = builder.Build();
app.UsePacketCapture();
```

Then resolve `IPacketCaptureManager` to inspect the captured request/response exchanges:

```c#
var capture = app.Services.GetRequiredService<IPacketCaptureManager>();
// read back the captured exchanges via `capture` (headers, status, timing, ...)
```

Capture can also be attached at the Kestrel level via `ListenOptions.UsePacketCapture(...)`.

> Because captured data accumulates in memory, keep capture scoped to short tests or targeted repros.
