# State: BridgeMod v0.3.0 (Phase 3 Foundation — Architectural Authority Established)

## Workspace Status

- **Branch:** main
- **Git Status:** Clean (Phase 3 Runtime committed locally; awaiting push to GitHub)
- **Current Version:** v0.4.0 (Stable locally)
- **Release Status:** ✅ v0.4.0 tagged locally; awaiting push and NuGet publication
- **Latest Commit:** `fd88330` — "Phase 3: Deterministic Behavior Graph Runtime introduced"
- **Previous:** `ca394f8` — "docs(ai): Update STATE.md with Phase 3 foundation completion"

## Build Status

### Current State
- ✅ `dotnet build` → **0 errors, 0 warnings** (Release)
- ✅ `dotnet test` → **80/80 tests passing** (11 Phase 1 + 20 Phase 2 + 15 Phase 3 Foundation + 34 Phase 3 Runtime)
- ✅ No new dependencies introduced

### Framework
- **SDK targets:** `net8.0` + `netstandard2.1` (broadest Unity/Mono compatibility)
- **Tests target:** `net10.0`
- **CI Workflow:** `.github/workflows/build.yml` → .NET 10.0 (all platforms) ✅

## Repository Structure (v0.3.0 + Phase 3 Foundation)

```
/src/BridgeMod.SDK          — Authoritative C# library (single source of truth)
  BridgeMod.Bridge.cs       — Phase 1: ModBridge, AuditLogger, BridgeConfig, ErrorCodes
  ModSurfaceCategory.cs     — Phase 2: enum Data, BehaviorGraphs, ProceduralInputs
  ModSurfaceStatus.cs       — Phase 2: enum Enabled, Limited, Disabled, Planned
  ModSurfaceDeclaration.cs  — Phase 2: sealed immutable surface record
  ModSurfaceRegistry.cs     — Phase 2: append-only host-level registry
  ModSurfaceSummaryGenerator.cs — Phase 2: pure capability matrix string generator
  ModContract.cs            — Phase 3: Canonical mod contract (immutable, additive)
  BehaviorGraphs.cs         — Phase 3: State machine executor (deterministic)
  IsExternalInit.cs         — C# 9+ polyfill for netstandard2.1
/src/DreamCraft.Engine       — Isolated Python simulation core
/samples/Legacies_Bridge_Test — End-to-end integration proof
/tests
  Phase1Tests.cs            — 11 firewall validation tests (unchanged)
  ModSurfaceTests.cs        — 20 Phase 2 surface metadata tests
  ModContractTests.cs       — 15 Phase 3 foundation tests
  ModBehaviorGraphTests.cs  — 34 Phase 3 runtime tests (determinism verified)
/docs
  Phase2_Mod_Surfaces.md    — Full architectural reference for Phase 2
  Engine_Adapter_Model.md   — Multi-engine support architecture
  Phase3_Runtime.md         — Phase 3 runtime specification and architecture
  Experimental_Notes.md     — Documents why schema registry is non-canonical
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

## Canonical Mod Contract: "Architectural Authority" (Phase 3 Foundation — Complete)

Foundational types for multi-engine support:
- **`ModContract` namespace** — Engine-agnostic canonical types (immutable, additive-safe)
- **`ModManifest`** — Authoritative mod package shape
- **`ModPayload`** — Validated mod data container
- **`ModConstraints`** — Deterministic guarantees declarations
- **`ValidationMetadata`** — Validation audit information
- **`ExecutionConstraints`** — Phase 3+ graph execution bounds (reserved for future use)

**Architectural Principle (Locked):** BridgeMod defines the contract; engines implement adapters. No engine defines validation rules upstream.

**Phase 3 Foundation Guarantee:** Canonical contract is stable and additive-only. No removals or renames for 5+ phases.

## What Exists (Verified)

### /src/BridgeMod.SDK (C# Library)
- ✅ 3-gate validation pipeline (Type Check → Boundary Guards → Audit Logging)
- ✅ `BridgeConfig` — configurable boundary guard settings
- ✅ `AuditLogger` — operational (in-memory)
- ✅ `ModSurfaceDeclaration` — sealed, immutable surface record
- ✅ `ModSurfaceRegistry` — append-only, duplicate-safe host registry
- ✅ `ModSurfaceSummaryGenerator` — pure capability matrix generator
- ✅ `ModContract` namespace (Phase 3 foundation)
  - `ModManifest` — canonical mod package shape
  - `ModPayload` — validated mod data container
  - `ModConstraints` — deterministic declarations
  - `ValidationMetadata` — validation audit trail
  - `ExecutionConstraints` — graph execution bounds (reserved)

### /src/DreamCraft.Engine (Python Simulation Core)
- ✅ Isolated from SDK; only consumes scrubbed JSON
- ✅ Safe defaults on blocked mods

### /samples/Legacies_Bridge_Test (Integration Proof)
- ✅ End-to-end validation verified

## Branch State

- **`main`** — Production. Phase 1 + Phase 2 + Phase 3 Foundation. All tests passing.
- **`experimental/dreamcraft-introspection`** — Renamed from `dev/phase-2-automation`. Contains optional schema registry tooling (non-canonical reference implementation). Does not affect main branch.
- **Stash:** `stash@{0}` preserved on `experimental/dreamcraft-introspection` for reference.
- Untracked files from experimental work (`src/BridgeMod.Generator/`, `src/BridgeMod.SDK/Generated/`, etc.) remain in working tree but are excluded from main branch compilation.

## Quality Metrics (v0.4.0 + Phase 3 Runtime)

| Metric | Value | Status |
|--------|-------|--------|
| Build Errors (Release) | 0 | ✅ |
| Compiler Warnings (Release) | 0 | ✅ |
| Test Pass Rate | 80/80 (100%) | ✅ |
| Phase 1 Tests | 11/11 | ✅ |
| Phase 2 Tests | 20/20 | ✅ |
| Phase 3 Foundation Tests | 15/15 | ✅ |
| Phase 3 Runtime Tests | 34/34 | ✅ |
| Public Members Documented | 100% | ✅ |
| New Dependencies Introduced | None | ✅ |
| Runtime Logic Modified | None | ✅ |
| Breaking Changes | None | ✅ |

## Phase 2 Status: 100% COMPLETE ✅

Developer Mod Surfaces: governance layer for declarative mod surface declarations. Fully implemented, tested, documented, committed.

## Phase 3 Foundation Status: 100% COMPLETE ✅

Canonical Mod Contract: foundational types for multi-engine support. Design specification drafted. Architecture locked. Implementation complete.

## Phase 3 Runtime Status: 100% COMPLETE ✅

Deterministic Behavior Graph Runtime: state machine executor with full determinism guarantee. Architecture specification locked. Implementation complete.

Key features:
- BehaviorGraphDefinition, BehaviorState, BehaviorTransition, TransitionGuard (sealed immutable types)
- BehaviorGraphValidator (pre-execution validation)
- BehaviorGraphExecutor (deterministic state transitions)
- 6 guard operators: Equals, NotEquals, GreaterThan, LessThan, GreaterThanOrEqual, LessThanOrEqual
- 22 comprehensive tests with determinism proof (1000-iteration verification)
- Full specification: `/docs/Phase3_Runtime.md`

## Next Phase

**Phase 4 — Procedural Control Layer (v0.5.0):**
- Procedural generation parameters and seed management
- ProceduralInputs surface category execution
- Must preserve all Phase 1 + Phase 2 + Phase 3 deterministic guarantees
- Status: Design pending

## Known Limitations (Intentional)

- ❌ No scripting support (Phase 3+ will provide deterministic graph execution)
- ❌ No asset replacement pipeline (future phase)
- ❌ No player-facing mod browser (Phase 5)
- ❌ No cloud backend (Phase 5; local-first model)
- ❌ Behavior graphs not yet executed (Phase 3 implementation pending)
- ⚠️ `AuditLogger` is in-memory only (file export deferred)
- ⚠️ Surface status enforcement is host responsibility — SDK does not act on status at runtime

## Guarantees (Locked)

- ✅ Deterministic execution — all validation produces identical results
- ✅ No reflection, dynamic loading, or scripting
- ✅ Canonical contract authority in SDK (not engines)
- ✅ Multi-engine compatible via adapter pattern
- ✅ Forward compatible (additive-only design)
