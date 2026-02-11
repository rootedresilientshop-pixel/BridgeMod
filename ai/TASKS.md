# Tasks

Inferred from repository contents and current verification.

## Active (in progress by evidence)

- Ongoing edits in SDK/test/tool csproj and source files (worktree modifications present).
- Core Phase 1 SDK functionality exists and is being iterated.

## Blocked or At Risk

- Solution-level build is blocked by missing executable entry points in tool projects.
- CI reliability is at risk due to framework mismatch (current `net10.0` target vs CI matrix 6/7/8).
- Documentation trust is at risk due to API and capability drift across multiple overlapping docs.

## Next Logical Tasks

1. Choose and document one framework support policy (for example, `net10.0` only, or multi-target).
2. Align all of these to that decision:
   - `sdk/BridgeMod.SDK.csproj`
   - `tests/BridgeMod.Tests.csproj`
   - tool csproj files
   - `.github/workflows/build.yml`
   - README/Quickstart/development docs
3. Resolve tool project state:
   - add `Program.cs` entry points and minimal CLI behavior, or
   - convert to libraries / remove from solution until implemented.
4. Reconcile documented API examples with actual SDK API surface.
5. Consolidate duplicate status/setup docs into canonical docs and archive or relocate redundant files.
6. Fix stale editor/debug configs (notably `.vscode/launch.json`).
7. Decide whether empty `docs/` and `cloud/` directories should contain planned placeholders with README notes or be removed until used.

## Candidate Cleanup Plan (requires approval before structural changes)

- Move release/community planning docs into `docs/` subfolders.
- Reduce root-level markdown set to canonical entry docs.
- Archive superseded docs with clear replacement links.

## Validation Task After Cleanup

- Run `dotnet build` at solution level and `dotnet test`; ensure both pass with aligned framework/tooling settings.