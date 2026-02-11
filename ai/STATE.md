# State

## Workspace Snapshot

- Git worktree is not clean.
- Modified files already existed before this archival pass:
  - `sdk/BridgeMod.SDK.csproj`
  - `sdk/Data/ModManifest.cs`
  - `sdk/Data/ModSchema.cs`
  - `sdk/PublicAPI/ModSurfaceDeclaration.cs`
  - `sdk/Runtime/ExecutionGuards.cs`
  - `sdk/Runtime/ModLoader.cs`
  - `sdk/Runtime/ModValidator.cs`
  - `tests/BridgeMod.Tests.csproj`
  - `tests/Phase1Tests.cs`
  - `tools/ModPackager/ModPackager.csproj`
  - `tools/SchemaValidatorCLI/SchemaValidator.csproj`
- Untracked folder present: `.claude/`

## What Exists

- SDK library:
  - Manifest and version parsing: `sdk/Data/ModManifest.cs`
  - Schema model/validation: `sdk/Data/ModSchema.cs`
  - Package validator: `sdk/Runtime/ModValidator.cs`
  - Loader/runtime mod representation: `sdk/Runtime/ModLoader.cs`
  - Execution safety helpers: `sdk/Runtime/ExecutionGuards.cs`
  - Public declaration API: `sdk/PublicAPI/ModSurfaceDeclaration.cs`
- Tests:
  - xUnit suite in `tests/Phase1Tests.cs` (26 tests currently passing)
- Examples:
  - API usage sample in `examples/Phase1Example.cs`
  - Sample mod package files under `examples/sample-mods/basic-balance-mod/`
- Tool projects:
  - `tools/ModPackager/ModPackager.csproj`
  - `tools/SchemaValidatorCLI/SchemaValidator.csproj`
  - No corresponding source entry points found.
- Repo/docs/governance/community files:
  - READMEs, roadmap, status docs, GitHub templates/workflow, changelog, conduct/contrib.

## What Works (verified)

- `dotnet test` succeeds for SDK + tests project.
- Tests report: 26 passed, 0 failed.
- `dotnet build sdk/BridgeMod.SDK.csproj` succeeds.
- `dotnet build tests/BridgeMod.Tests.csproj` succeeds.

## What Is Broken or Inconsistent (verified)

- Full solution build fails because tool projects are executable projects without `Main`:
  - `tools/ModPackager/ModPackager.csproj` -> `CS5001`
  - `tools/SchemaValidatorCLI/SchemaValidator.csproj` -> `CS5001`
- Framework/version messaging conflict:
  - Project files target `net10.0`.
  - Docs and CI messaging repeatedly claim `.NET 6.0+` support.
  - `.github/workflows/build.yml` matrix tests 6/7/8, not 10.
- API documentation conflicts with code:
  - Docs/examples reference methods not present in code (`GetContentAsJson`, `ValidateFileAccess`, `EnforceExecutionTimeout`).
- VS Code debug config likely stale:
  - `.vscode/launch.json` points to `${workspaceFolder}/bin/Debug/net6.0/BridgeMod.dll`, which does not match current project outputs/layout.
- `docs/` and `cloud/` folders exist but are effectively placeholders (no tracked implementation docs/service code).

## Compiler Warnings (from test build)

- Nullable warnings in SDK (e.g., in `sdk/Data/ModSchema.cs`, `sdk/Runtime/ModValidator.cs`).
- Extensive XML documentation warnings because `GenerateDocumentationFile=true` while many public members lack XML comments.

## Suspected Obsolete, Duplicate, or Conflicting Files

### High-overlap narrative/status docs (candidates to merge)

- `SETUP_COMPLETE.md`
- `IMPLEMENTATION_STATUS.md`
- `README_DEVELOPMENT.md`
- `GITHUB_PREP_SUMMARY.md`

These repeat architecture/status claims, counts, and setup guidance with drift between files.

### Potentially stale release/setup guidance (candidate review or archive)

- `GITHUB_RELEASE_CHECKLIST.md`
- `DISCUSSIONS_STRATEGY.md`
- `CROWDFUNDING.md`

These are useful, but operationally separate from SDK/runtime implementation and may belong under a dedicated `docs/release/` or `docs/community/` area.

### Config files with likely drift (candidate update or retire)

- `.vscode/launch.json` (output path mismatch)
- Portions of docs that describe APIs not implemented in current code

## Proposed Merge/Delete Actions (not executed)

1. Merge status/implementation docs into one canonical technical status doc and one developer guide.
2. Move release/community strategy docs under `docs/release/` and `docs/community/`; keep root focused on product docs.
3. Either:
   - implement tool entry points, or
   - change tool projects to class libraries until implemented, or
   - remove them from solution temporarily.
4. Align target framework, docs, and CI matrix to a single supported baseline.
5. Update or remove stale VS Code launch config.