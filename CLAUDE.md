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
