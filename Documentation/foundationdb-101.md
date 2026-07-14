# How it connects

FoundationDB has a client/server split that trips up almost every newcomer, so it is worth two minutes before you write any code. Get this right and the rest is easy; get it wrong and every operation mysteriously times out.

## Three moving parts

```mermaid
flowchart LR
    app["Your .NET app"] --> managed["FoundationDB.Client<br/>(managed binding)"]
    managed --> native["fdb_c<br/>(native library)"]
    native -->|"network"| cluster[("FoundationDB<br/>cluster")]
```

- **`FoundationDB.Client`** is the managed .NET binding: the API you write against. It does not talk to the cluster directly.
- **`fdb_c`** is the native client library it loads, shipped by the `FoundationDB.Client.Native` package. This is what actually speaks the wire protocol to the cluster.
- **The cluster** is the running FoundationDB server(s).

## The one rule

The **native `fdb_c` library must be protocol-compatible with the cluster.** A `7.4` native client cannot talk to a `7.3` cluster, and vice versa: FoundationDB's wire protocol changes between minor versions.

There are two version knobs, and only one of them is about the cluster:

| Package / setting | What it controls | Rule |
|---|---|---|
| `FoundationDB.Client` (managed) | The API you can call | Use the latest. It does not tie you to a cluster version. |
| `FoundationDB.Client.Native` | The native `fdb_c`, i.e. the wire protocol | **Must match your cluster.** `7.3.x` for a `7.3` cluster. |
| API level (`Fdb.Start(730)`, `AddFoundationDb(730, ...)`) | The feature and behaviour level | At or below the cluster's level. `<= 730` for `7.3`; a `7.4+` cluster allows up to `740`. |

So a `7.3` cluster is served perfectly well by the latest `FoundationDB.Client` + `FoundationDB.Client.Native` `7.3.x` + API level `730`.

## What a mismatch looks like

```mermaid
flowchart TB
    subgraph ok["Matching versions: works"]
        direction LR
        a1["fdb_c 7.3"] -->|"connects"| c1[("7.3 cluster")]
    end
    subgraph bad["Mismatched versions: every op times out"]
        direction LR
        a2["fdb_c 7.4"] -.->|"never connects, keeps retrying"| c2[("7.3 cluster")]
    end
```

When the native client and the cluster do not match, the coordinators are reachable but rejected as incompatible. The client keeps retrying, and the only thing your code sees is a **transaction timeout**. Meanwhile `fdbcli` (which uses whatever client you installed system-wide, often a matching one) works fine, which makes it look like your app is broken when it is really a version mismatch.

To tell them apart, compare the versions. The cluster side:

```console
fdbcli --version
```

The app side: `Fdb.GetClientVersion()` returns the loaded native version and protocol string. If the protocol strings differ, that is your problem.

## Why pin the native client per project

Shipping `fdb_c` through the `FoundationDB.Client.Native` NuGet package, instead of a machine-wide install, means each project locks its own native version. You can keep a `7.3` branch and a `7.4` branch of the same application building and testing side by side, each against its own cluster. A single system-wide client cannot do that.

## Next

- [Cluster setup](cluster-setup.md): connect to an existing cluster and pick the matching native version, or spin one up locally.
