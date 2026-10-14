# AGENTS.md

GenMate's cross-line rules are in the genmate-factory repo's `AGENTS.md` (`D:\src\GenMate\AGENTS.md`), which a session in a treehouse worktree does not load on its own.

## Build & test

`./build.ps1 -Test` builds and tests; `./build.ps1 -Launch` launches. Never `dotnet run` (`build.ps1` header says why).

## Releasing: merging to `main` IS the release

Feature PRs target `main`; `develop` is stale (`build-qa.yml`).

## Agent skills

- **Issue tracker** — `docs/agents/issue-tracker.md`
- **Triage labels** — `docs/agents/triage-labels.md`
- **Domain docs** — `docs/agents/domain.md`

## Maintaining this file

Governed by `~/.claude/CLAUDE.md` → *Agent instruction files*. Code comments carry only what the code beside them cannot say: a reason, a non-local consequence, a deliberate removal, a load-bearing warning, an external constraint or an invariant the type cannot express, never a restatement; one whose rule a test pins says so in a clause. Shorten a comment rather than delete it, and when unsure, keep it.
