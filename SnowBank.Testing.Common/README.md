SnowBank.Testing.Common
=======================

Base library for writing [NUnit](https://nunit.org/) tests with the SnowBank SDK.

# Concept

Derive your test fixtures from `SimpleTest` to get a consistent base for SnowBank-based tests: structured logging captured per test, helpers for assertions and for dumping values, deterministic setup (invariant culture, one-time warmup), a test-scoped cancellation token, and access to the SDK's testing conveniences such as virtual time and scoped CPU-load injection for reproducing load-dependent flakes.

It has no FoundationDB dependency: it is the shared NUnit base used across the SnowBank and FoundationDB test suites, and the foundation the higher-level `SnowBank.Testing.Framework` distributed-test harness builds on.

# How to use

```
dotnet add package SnowBank.Testing.Common
```

```c#
public class MyFacts : SimpleTest
{
    [Test]
    public void Values_Round_Trip()
    {
        // `Log(...)` output is captured and attached to this test only
        Log("checking round-trip");

        var slice = Slice.FromStringUtf8("hello");
        Assert.That(slice.ToStringUtf8(), Is.EqualTo("hello"));
    }
}
```

`SimpleTest` also exposes the cancellation token bound to the test's lifetime and the virtual-time helpers used throughout the SDK's suites.

# Testing distributed systems?

This package is the base for plain, single-process NUnit tests. When you need to test **distributed** behavior (several hosts talking to each other: web hosts, agents, even a real browser), reach for the companion **`SnowBank.Testing.Framework`** package. It builds on this one and lets you stand up a whole topology on an in-memory virtual network inside a single test.
