# AGENTS.md

This file provides guidance to coding agents working in this repository.

## What this is

GenMate.PluginInstaller is a desktop app that installs the GenMate plugin for AutoCAD 2024 and BricsCAD V24. It is part of the larger GenMate ecosystem (see parent `GenMate/CLAUDE.md` for full architecture).

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

**Write only what the code cannot say.** Before adding a block, ask whether an agent could learn it from the code it would naturally open. If it could, do not write it, and name the file, symbol, config key or test that answers it instead. If a rule binds at a call site, put it in a comment there or in a test that fails when it is broken, and not here. This applies to code comments too: a comment that restates the code beneath it, repeats a member's name, narrates the steps, or explains standard language semantics should not be written, while one carrying a reason, a non-local consequence, a deliberate removal, a warning that something which looks removable is load-bearing, an external constraint, or a unit or invariant the type cannot express should. A comment whose rule is already pinned by a test says so in one clause and stops. Prefer shortening a comment to its load-bearing clause over deleting it, and when unsure whether a comment is protected, keep it. Doc comments that ship in a published package, and comments a tool reads, are out of scope. What belongs here is what has no point of change: something built and then deliberately removed, an accepted loss or a deliberate one-way migration, a rejected alternative and its reason, or a contract that is invisible in the repo where it gets violated. Never remove one of these, and write one when your change creates it. When unsure whether a block is one of those, keep it. Do not add a repository overview, directory tree, technology list, file inventory, command list, or service roster. They go stale silently and the tooling answers them accurately.

Never state a fact in two places. If a change makes a documented fact stale, update its single owner; if the code now states it, delete the prose.
