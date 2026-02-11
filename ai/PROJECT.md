# Project

BridgeMod is a C#/.NET SDK for developer-controlled, safety-first game mod loading.

## Identity

- Name: BridgeMod
- Domain: game modding SDK/runtime + documentation/tooling
- Language/runtime: C# on .NET (currently `net10.0` in project files)
- License: MIT

## Goals (supported by repository contents)

- Load ZIP-based mods with `manifest.json` metadata.
- Validate mod packages locally before loading.
- Treat mods as untrusted input and isolate failures.
- Let game developers declare allowed mod surfaces for modders.
- Provide docs/examples for developer integration.

## Non-goals (current repository state)

- No runtime scripting execution.
- No asset replacement pipeline.
- No player-facing mod browser.
- No required cloud dependency for runtime validation.

## Constraints

- Safety constraints are central (validation, bounded behavior, disable-on-error).
- API/docs consistency is currently incomplete and needs consolidation.
- Solution currently includes scaffolded tool projects that do not compile as executables yet.
- Repo carries both product docs and release/community prep docs, with overlap.