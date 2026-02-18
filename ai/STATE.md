# State: BridgeMod v0.3.0 (Phase 2 — Developer Mod Surfaces — Complete)

## Workspace Status

- **Branch:** main
- **Git Status:** Clean (all Phase 2 changes committed and pushed)
- **Current Version:** v0.3.0 (Stable)
- **Release Status:** ✅ LIVE on GitHub; tag `v0.3.0` pushed
- **Commit:** `ffa5764` — "Phase 2: Introduce Developer Mod Surface Declarations"

## Build Status

### Current State
- ✅ `dotnet build` → **0 errors, 0 warnings** (Release)
- ✅ `dotnet test` → **31/31 tests passing** (11 Phase 1 + 20 Phase 2)
- ✅ No new dependencies introduced

### Framework
- **SDK targets:** `net8.0` + `netstandard2.1` (broadest Unity/Mono compatibility)
- **Tests target:** `net10.0`
- **CI Workflow:** `.github/workflows/build.yml` → .NET 10.0 (all platforms) ✅

## Repository Structure (v0.3.0)

```
/src/BridgeMod.SDK          — Authoritative C# library (single source of truth)
  BridgeMod.Bridge.cs       — Phase 1: ModBridge, AuditLogger, BridgeConfig, ErrorCodes
  ModSurfaceCategory.cs     — Phase 2: enum Data, BehaviorGraphs, ProceduralInputs
  ModSurfaceStatus.cs       — Phase 2: enum Enabled, Limited, Disabled, Planned
  ModSurfaceDeclaration.cs  — Phase 2: sealed immutable surface record
  ModSurfaceRegistry.cs     — Phase 2: append-only host-level registry
  ModSurfaceSummaryGenerator.cs — Phase 2: pure capability matrix string generator
  IsExternalInit.cs         — C# 9+ polyfill for netstandard2.1
/src/DreamCraft.Engine       — Isolated Python simulation core
/samples/Legacies_Bridge_Test — End-to-end integration proof
/tests
  Phase1Tests.cs            — 11 firewall validation tests (unchanged)
  ModSurfaceTests.cs        — 20 Phase 2 surface metadata tests
/docs
  Phase2_Mod_Surfaces.md    — Full architectural reference for Phase 2
```

> **Note:** `tests/BridgeMod.Tests.csproj` excludes `tests/Automation/` (belongs to dev/phase-2-automation branch; not part of main).

## Security Architecture: "Firewall Standard" (Phase 1 — Unchanged)

3-gate C# validation pipeline:
1. **Type Check** — Validates input shape/types
2. **Boundary Guards** — Configurable via `BridgeConfig` (e.g., clamps 999999+ health values)
3. **Audit Logging** — Operational; records all validation decisions

**Air-Gap Strategy:** The C# SDK validates data. The Python Engine (`DreamCraft.Engine`) only consumes "scrubbed" JSON. The Engine falls back to safe defaults if the SDK blocks a mod.

## Governance Layer: "Developer Mod Surfaces" (Phase 2 — Complete)

Host-level mod exposure boundary system:
- **`ModSurfaceRegistry`** — Game developer registers surfaces at initialization. Append-only. Mods have no access.
- **`ModSurfaceDeclaration`** — Sealed, immutable record: Name, Category, Status, Description. No setters, no mutation.
- **`ModSurfaceStatus`** — Enabled / Limited / Disabled / Planned. Governance metadata only — no runtime enforcement by SDK.
- **`ModSurfaceCategory`** — Data / BehaviorGraphs / ProceduralInputs. Drives capability matrix grouping.
- **`ModSurfaceSummaryGenerator`** — Pure string generator. No I/O. Groups by category, orders alphabetically.

**Phase 2 Runtime Guarantee:** Zero runtime execution changes. Deterministic guarantees from Phase 1 fully preserved.

## What Exists (Verified)

### /src/BridgeMod.SDK (C# Library)
- ✅ 3-gate validation pipeline (Type Check → Boundary Guards → Audit Logging)
- ✅ `BridgeConfig` — configurable boundary guard settings
- ✅ `AuditLogger` — operational (in-memory)
- ✅ `ModSurfaceDeclaration` — sealed, immutable surface record
- ✅ `ModSurfaceRegistry` — append-only, duplicate-safe host registry
- ✅ `ModSurfaceSummaryGenerator` — pure capability matrix generator

### /src/DreamCraft.Engine (Python Simulation Core)
- ✅ Isolated from SDK; only consumes scrubbed JSON
- ✅ Safe defaults on blocked mods

### /samples/Legacies_Bridge_Test (Integration Proof)
- ✅ End-to-end validation verified

## Stash State

- **`dev/phase-2-automation` WIP** is stashed on the `dev/phase-2-automation` branch.
- Untracked files from that branch (`src/BridgeMod.Generator/`, `src/BridgeMod.SDK/Generated/`, `src/BridgeMod.SDK/SchemaRegistry.cs`, `tests/Automation/`) remain in the working tree but are excluded from main branch compilation.

## Quality Metrics (v0.3.0)

| Metric | Value | Status |
|--------|-------|--------|
| Build Errors (Release) | 0 | ✅ |
| Compiler Warnings (Release) | 0 | ✅ |
| Test Pass Rate | 31/31 (100%) | ✅ |
| Phase 1 Tests Unchanged | 11/11 | ✅ |
| Phase 2 Tests Added | 20/20 | ✅ |
| Public Members Documented | 100% | ✅ |
| New Dependencies Introduced | None | ✅ |
| Runtime Logic Modified | None | ✅ |

## Phase 2 Status: 100% COMPLETE

The governance layer for mod surface declarations is implemented, tested, documented, committed, and pushed.

## Next Phase

**Phase 3 — Behavior Graph Runtime:**
- Deterministic graph executor for state machines and ECA rule graphs
- No scripting — declarative node graphs only
- Must preserve all Phase 1 + Phase 2 deterministic guarantees
- Target version: v0.4.0

## Known Limitations (Intended)

- No scripting support (Phase 3+)
- No asset replacement pipeline (future phase)
- No player-facing mod browser (Phase 5)
- No cloud backend (Phase 5; local-only)
- `AuditLogger` is in-memory only (file export deferred)
- Surface status enforcement is host responsibility — SDK does not act on status at runtime
