# Prerequisites

This page lists what you need before connecting to FoundationDB from .NET, and how to check each piece works. If you already have a .NET 10 project and access to a cluster, skip ahead to [Getting Started](getting-started.md).

You need three things:

1. **The .NET 10 SDK (or newer).** [Install for your platform](https://dotnet.microsoft.com/download).
2. **Docker**, if you want to run a FoundationDB cluster locally. [Install for your platform](https://docs.docker.com/get-docker/). If you already have a cluster to connect to, you can skip this.
3. **The native `fdb_c` client library.** You do *not* install this yourself: the [`FoundationDB.Client.Native`](https://www.nuget.org/packages/FoundationDB.Client.Native) NuGet package ships it for you, on Windows, Linux and macOS. Choosing the right version is covered in [Cluster setup](cluster-setup.md).

> FoundationDB's native client is **64-bit only**. Your .NET process must run as 64-bit (the default on modern runtimes).

## Check your setup

Run these three commands. For each, here is what success looks like and what the common failures mean.

**Check the .NET SDK:**

```console
dotnet --info
```

You should see a list of installed SDKs with at least one `10.x` entry:

```
.NET SDKs installed:
  10.0.100 [/usr/share/dotnet/sdk]
```

- `command not found` (or `'dotnet' is not recognized` on Windows): the SDK is not installed, or not on your `PATH`. Install it, then open a new terminal.
- Only older versions are listed (for example `8.0.x`): install the .NET 10 SDK.

**Check Docker is installed and running:**

```console
docker version
```

You should see both a `Client` and a `Server` section, each with a version:

```
Client:
 Version:    27.x.x
Server: Docker Desktop
 Engine:
  Version:   27.x.x
```

- `command not found` (or `'docker' is not recognized`): Docker is not installed, or not on your `PATH`.
- `Cannot connect to the Docker daemon` (or you only see the `Client` section): Docker is installed but not running. Start Docker Desktop (Windows/macOS) or run `sudo systemctl start docker` (Linux), wait a few seconds, and retry.

**Check Docker can pull and run an image:**

```console
docker run --rm hello-world
```

You should see a short message ending with:

```
Hello from Docker!
This message shows that your installation appears to be working correctly.
```

- It hangs and you have to press `Ctrl-C`: the pull could not reach Docker Hub. Check your network or proxy, then retry.
- `permission denied while trying to connect to the Docker daemon socket` (Linux): your user is not in the `docker` group. Add it with `sudo usermod -aG docker $USER`, then log out and back in, or prefix commands with `sudo`.

## Next

- [How it connects](foundationdb-101.md): a two-minute mental model of the client, the native library, and the cluster. Read this before writing code; it explains the pitfall that catches almost every newcomer.
