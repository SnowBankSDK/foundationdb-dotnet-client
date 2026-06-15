# foundationdb-dotnet-skills (Claude Code plugin)

This plugin packages the repository's [Agent Skills](https://agentskills.io) so they can be installed by developers who use the FoundationDB .NET client (via NuGet or as a submodule):

```text
/plugin marketplace add SnowBankSDK/foundationdb-dotnet-client
/plugin install foundationdb-dotnet-skills@snowbank
```

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
