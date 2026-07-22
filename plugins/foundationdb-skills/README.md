# foundationdb-dotnet-skills (Claude Code plugin)

This plugin packages the repository's [Agent Skills](https://agentskills.io) so they can be installed by developers who use the FoundationDB .NET client (via NuGet or as a submodule):

```text
/plugin marketplace add SnowBankSDK/foundationdb-dotnet-client
/plugin install foundationdb-dotnet-skills@snowbank
```

## The skills

| Skill | Read it when… |
|---|---|
| **foundationdb-keys-and-layers** | encoding keys or values, building tuple keys, resolving a subspace or Directory-layer path, designing a key layout, or writing a class that stores data in FoundationDB (a "Layer"). **Start here** — key encoding is the decision everything else is built on. |
| **foundationdb-transactions** | opening a transaction, writing a read-modify-write, incrementing a counter atomically, waiting on a watch, or diagnosing a conflict / retry / `TransactionTooOld`. |
| **foundationdb-advanced-layers** | building something performance-sensitive or distributed: batching reads, avoiding contention, change feeds, version-stamp logs, leases, retention, fencing. Builds on the two above. |
| **foundationdb-aspire** | standing up a cluster and connecting to it: the Aspire hosting and client integrations, the native `libfdb_c`, and client ⇄ cluster version compatibility. This is how you get the `IFdbDatabaseProvider` the other skills assume you already have. |
| **snowbank-slices-and-buffers** | working with `Slice`, `SliceReader`/`SliceWriter`, spans or pooled buffers — the binary layer underneath every key and value. |
| **crystaljson** | parsing, building, mutating or serializing JSON with `SnowBank.Data.Json`, declaring a generated converter or proxy, or migrating from `DataContractJsonSerializer` / Newtonsoft. |
| **snowbank-betterhttp** | making outbound HTTP calls: DI policy bundles, TLS and certificates, typed protocols, or porting legacy `HttpWebRequest` code. |
| **snowbank-distributed-testing** | writing or, especially, **diagnosing** multi-node integration tests on the SnowBank test framework, and reading its unified test journal. |

The first four are the FoundationDB client proper; the last four are the general-purpose `SnowBank.*` libraries it is built on, useful on their own.

## ⚠️ `skills/` is generated — do not edit it directly

The canonical skills live in **[`/.claude/skills/`](../../.claude/skills/)** (the single source of truth, which also auto-loads for agents working inside this repo). The `skills/` folder here is a **committed copy** of that directory — committed because the plugin marketplace reads the repo at a commit, with no build step on the consumer side.

After editing anything under `/.claude/skills/`, re-sync this copy:

```bash
# macOS / Linux (or Git Bash on Windows)
scripts/sync-plugin-skills.sh            # update the copy
scripts/sync-plugin-skills.sh --check    # verify in sync (used by CI)
```

```powershell
# Windows (Windows PowerShell 5.1 or pwsh 7+)
./scripts/sync-plugin-skills.ps1
./scripts/sync-plugin-skills.ps1 -Check
```

(A committed symlink was avoided on purpose — it does not resolve reliably across Windows/macOS/Linux checkouts.)

## Releasing

Bump `version` in both `.claude-plugin/plugin.json` (here) and `/.claude-plugin/marketplace.json` whenever the skills change — Claude Code detects "update available" from that version string, so an unchanged version ships no update to existing users.
