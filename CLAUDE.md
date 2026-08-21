# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

GenMate.PluginInstaller is a desktop app that installs the GenMate AutoCAD plugin. It is part of the larger GenMate ecosystem (see parent `GenMate/CLAUDE.md` for full architecture).

## Build & Run

Build and launch with `./build.sh` — see the header comment and flag parsing in that script. Do not use `dotnet run`: the build runs under WSL but the app can only launch under the Windows .NET desktop runtime, which `build.sh -l` handles.

## Architecture Notes

- **No DI container** — services are instantiated directly with `new` in `MainWindow` for simplicity. If the app grows more complex, reevaluate and consider introducing a DI container.

## Agent skills

- **Issue tracker** — `docs/agents/issue-tracker.md`
- **Triage labels** — `docs/agents/triage-labels.md`
- **Domain docs** — `docs/agents/domain.md`

## Maintaining this file

When you pin a rule with a test or state it in a code comment, delete the prose that said it. A block that names a test is a block whose job is done.

**Write only what the code cannot say.** Before adding a block, ask whether an agent could learn it from the code it would naturally open. If it could, do not write it. If a rule binds at a call site, put it in a comment there or in a test that fails when it is broken, and not here. What belongs here is what has no point of change: something built and then deliberately removed, an accepted loss, a rejected alternative and its reason, or a contract that is invisible in the repo where it gets violated. Do not add a repository overview, directory tree, technology list, file inventory, command list, or service roster. They go stale silently and the tooling answers them accurately.

The no-mistakes Document step never reads this file — it runs with `CLAUDE.md` neutralized. The same policy is stated for it in `document.instructions` in `.no-mistakes.yaml`, read only from the default branch. Change both together.
