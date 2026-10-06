# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

## This repo is one context

GenMate is split into contexts, listed in the [glossary map](https://github.com/jperna7254/genmate-datamodels/blob/main/GLOSSARY-MAP.md) in genmate-datamodels. This repo is the whole of the **Plugin installation** context, a generic one: it shares no vocabulary with any other context.

Its words (channel, host, bundle asset, self-update) mean nothing elsewhere in GenMate, and GenMate's words (block, circuit, material, quote) mean nothing here. Do not read this repo's words through the shared glossary in genmate-datamodels, and do not add them to it.

## Before exploring, read these

- **`GLOSSARY.md`** at the repo root, if it exists.
- **`docs/adr/`**: read ADRs that touch the area you're about to work in.

This repo gets a `GLOSSARY.md` only when one of its words needs defining. If either of these is missing, **proceed silently**. Don't flag the absence and don't suggest creating it upfront. The `/domain-modeling` skill creates it when a term actually gets resolved.

## Use the glossary's vocabulary

When your output names a concept `GLOSSARY.md` defines (in an issue title, a refactor proposal, a test name), use its term, not a synonym it avoids.

## Flag ADR conflicts

If your output contradicts an existing ADR, surface it explicitly rather than silently overriding:

> _Contradicts ADR-0007 (event-sourced orders), but worth reopening because…_
