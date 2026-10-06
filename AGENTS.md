# AGENTS.md

What spans the five GenMate repos is in `~/src/GenMate/AGENTS.md`, which a session in a treehouse worktree does not load on its own.

## Build & test

`./build.sh -t` builds and tests; `./build.sh -l` launches. Never `dotnet run` (`build.sh` header says why).

## Releasing: merging to `main` IS the release

Feature PRs target `main`; `develop` is stale (`build-qa.yml`). A merge publishes only when `Version` in the csproj has no tag yet, so a PR meant to ship bumps it, and is not done until `gh-axi api repos/jperna7254/genmate-plugin-installer/releases/tags/v<Version> --jq '.assets[].name'` lists `GenMate.PluginInstaller.exe`.

## Agent skills

- **Issue tracker** — `docs/agents/issue-tracker.md`
- **Triage labels** — `docs/agents/triage-labels.md`
- **Domain docs** — `docs/agents/domain.md`

## Maintaining this file

Governed by `~/.claude/CLAUDE.md` → *Agent instruction files*. Code comments carry only what the code beside them cannot say: a reason, a non-local consequence, a deliberate removal, a load-bearing warning, an external constraint or an invariant the type cannot express, never a restatement; one whose rule a test pins says so in a clause. Shorten a comment rather than delete it, and when unsure, keep it.
