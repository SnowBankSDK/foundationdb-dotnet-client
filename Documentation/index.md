# SnowBank SDK

The SnowBank SDK is a set of .NET libraries for building distributed applications with FoundationDB as the main database. It groups into two families:

- **[SnowBank](snowbank/index.md)**: general-purpose .NET libraries, shared across the SDK and usable on their own. `SnowBank.Core` holds the tools almost every package needs: binary slices and buffers, the tuple encoding, and the CrystalJson serializer. Every type lives under the `SnowBank` namespace root.
- **[FoundationDB](fdb/introduction.md)**: the .NET client for [FoundationDB](https://www.foundationdb.org/), the distributed ordered key/value store. `FoundationDB.Client` connects to a cluster, and can run against the FakeDb emulator or the FdbLite engine for tests and local use. Reference Layers add higher-level data models on top.

## How the pieces fit

The SDK is two stacks side by side. On the left, `FoundationDB.Client` over its backends (the native `fdb_c` client, plus `FdbLite` and `FakeDb` for tests and local use), talking to a FoundationDB cluster. On the right, `SnowBank.Core` over its components (slices, JSON, HTTP, testing), on the .NET runtime. Layers and your application build on top, and the whole runs on Aspire for local development, or Docker, Kubernetes, or bare metal in production.

<div class="arch" role="img" aria-label="Two component stacks side by side under one application. Left, the FoundationDB stack: FoundationDB.Client over its backends fdb_c, FdbLite and FakeDb, on a FoundationDB cluster. Right, the SnowBank stack: SnowBank.Core over its components Slice, JSON, Http and Testing, on the .NET runtime. Layers and NuGet packages sit above, your application on top, and the whole runs on Aspire, AWS, Azure, Kubernetes, bare metal or another host.">
<style>
.arch { width: 100%; margin: 1.4rem 0; display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
.arch .cell { border: 1px solid var(--bs-border-color); border-radius: 8px; background: var(--bs-secondary-bg); color: var(--bs-emphasis-color); padding: 0.7rem 0.85rem; text-align: center; font-weight: 600; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 0.35rem; }
.arch .sub { color: var(--bs-secondary-color); font-weight: 400; font-size: 0.86em; }
.arch .span2 { grid-column: 1 / -1; }
.arch .app { background: #1e5aa8; color: #fff; border-color: #1e5aa8; font-size: 1.35rem; font-weight: 700; padding: 0.9rem; }
.arch .chips { display: flex; flex-wrap: wrap; gap: 6px; justify-content: center; }
.arch .chips span { border: 1px solid var(--bs-border-color); border-radius: 6px; background: var(--bs-body-bg); color: var(--bs-body-color); padding: 0.25rem 0.6rem; font-weight: 500; font-size: 0.9em; }
.arch .group { padding: 0; gap: 0; }
.arch .group .part { width: 100%; padding: 0.65rem 0.85rem; display: flex; flex-direction: column; align-items: center; gap: 0.4rem; }
.arch .group .part + .part { border-top: 1px dashed var(--bs-border-color); }
.arch .env { background: transparent; border-style: dashed; }
.arch .hosting { grid-column: 1 / -1; flex-direction: row; align-items: stretch; padding: 0; }
.arch .hosting .host { flex: 1; padding: 0.55rem 0.35rem; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 0.3rem; text-align: center; font-size: 1rem; }
.arch .hosting .host img { width: 32px; height: 32px; }
.arch .hosting .host + .host { border-left: 1px dashed var(--bs-border-color); }
.arch .cell.logo { flex-direction: row; gap: 0.5rem; }
.arch .cell.logo img { width: 40px; height: 40px; flex: none; }
.arch .cell.logo img.net { transform: translateY(1.5px); } /* the .NET wordmark's dot drags its box center below the caps; drop it so the caps sit level with the text */
</style>
<div class="cell app span2">Your application</div>
<div class="cell">Layers <span class="sub">Map, Index, Queue, ...</span></div>
<div class="cell">NuGet packages, helper libraries</div>
<div class="cell group">
<div class="part">FoundationDB.Client</div>
<div class="part"><div class="chips"><span>fdb_c</span><span>FdbLite</span><span>FakeDb</span></div></div>
</div>
<div class="cell group">
<div class="part">SnowBank.Core</div>
<div class="part"><div class="chips"><span>Slice</span><span>JSON</span><span>Http</span><span>Testing</span></div></div>
</div>
<div class="cell env logo"><img src="images/foundationdb.svg" alt=""> FoundationDB cluster</div>
<div class="cell env logo"><img src="images/dotnet.svg" alt="" class="net"> Runtime</div>
<div class="cell env hosting span2">
<div class="host"><img src="images/aspire.svg" alt=""> Aspire</div>
<div class="host"><img src="images/aws.svg" alt=""> AWS</div>
<div class="host"><img src="images/azure.svg" alt=""> Azure</div>
<div class="host"><img src="images/kubernetes.svg" alt=""> Kubernetes</div>
<div class="host"><img src="images/bare-metal.svg" alt=""> Bare metal</div>
<div class="host">...</div>
</div>
</div>

## SnowBank: general-purpose libraries

Building blocks used across the SDK and usable without FoundationDB. Start at **[The SnowBank libraries](snowbank/index.md)**.

- **[Slices and buffers](snowbank/slices-and-buffers.md)**: `Slice`, a read-only view over bytes, with `SliceReader` and `SliceWriter`, pooled buffers, and the integer encodings.
- **[Tuples](snowbank/tuples.md)**: `TuPack`, the tuple encoding that turns typed values into bytes whose order matches the values.
- **[JSON](snowbank/crystaljson/index.md)**: CrystalJson, an in-memory JSON model (`JsonValue`) with a source generator for reflection-free serializers.

## FoundationDB: the database client

The .NET client for FoundationDB. Start at **[Introduction](fdb/introduction.md)** if you came for the database.

- **[Setup](fdb/prerequisites.md)**: install .NET and a local cluster, and match the native client to your cluster version.
- **[Getting Started](fdb/getting-started.md)**: your first read and write, then a small HTTP API you can click through.
- **[Aspire](fdb/aspire/index.md)**: let .NET Aspire start the cluster and inject the connection.
- **[Guide](fdb/guide/index.md)**: keys and Layers, transactions, and the advanced distributed patterns.

## API reference

The **[API reference](api/index.md)** has one page per public type, across the documented assemblies: `SnowBank.Core`, `FoundationDB.Client`, `FoundationDB.Aspire`, and `FoundationDB.Aspire.Hosting`.
