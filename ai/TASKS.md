# Tasks: BridgeMod (Active & Planned)

---

## ✅ Phase 1: Security Foundation — COMPLETE (v0.2.4)

### ✅ Firewall Standard Architecture
- [x] Design and implement 3-gate C# validation pipeline (Type Check → Boundary Guards → Audit Logging)
- [x] Implement `BridgeConfig` for configurable boundary guards
- [x] Implement `AuditLogger` (in-memory, operational)
- [x] Verify script injection is blocked without crashing the Python engine
- [x] Verify out-of-bounds stats (999999+ health) are clamped

### ✅ Air-Gap Architecture
- [x] Isolate `DreamCraft.Engine` (Python) from raw mod data
- [x] Ensure Python Engine only consumes scrubbed JSON from C# SDK
- [x] Implement safe defaults in Python Engine when SDK blocks a mod

### ✅ Repository Overhaul (v0.2.4)
- [x] Move to professional `/src/` + `/samples/` layout
- [x] Replace legacy `/sdk/` with `/src/BridgeMod.SDK` as single source of truth
- [x] Publish BridgeMod.SDK v0.2.4 to NuGet and verify NuGet sync

### Quality (Phase 1 Baseline)
- [x] 0 build errors, 0 compiler warnings (Release build)
- [x] 11/11 Phase 1 tests passing
- [x] 100% public API documented

---

## ✅ Phase 2: Developer Mod Surfaces — COMPLETE (v0.3.0)

### ✅ Core Types Added
- [x] `ModSurfaceCategory.cs` — enum: Data, BehaviorGraphs, ProceduralInputs
- [x] `ModSurfaceStatus.cs` — enum: Enabled, Limited, Disabled, Planned
- [x] `ModSurfaceDeclaration.cs` — sealed immutable record; ArgumentException on null/empty name or description
- [x] `ModSurfaceRegistry.cs` — append-only host registry; InvalidOperationException on duplicate name; IReadOnlyList exposure
- [x] `ModSurfaceSummaryGenerator.cs` — pure string generator; no I/O; groups by category alphabetically; orders surfaces alphabetically

### ✅ Tests
- [x] `tests/ModSurfaceTests.cs` — 20 tests covering:
  - Duplicate surface registration throws
  - Null registration throws
  - Empty/null/whitespace name throws
  - Empty description throws
  - Summary groups by category correctly
  - Summary orders surfaces alphabetically within category
  - Summary orders categories alphabetically
  - Status appears in output
  - Title appears in output
  - Empty registry returns notice string

### ✅ Documentation
- [x] README.md — Phase 2 section: categories table, status table, example matrix, no-scripting statements
- [x] `docs/Phase2_Mod_Surfaces.md` — full architectural reference

### ✅ Release
- [x] SDK version bumped to 0.3.0 in `BridgeMod.SDK.csproj`
- [x] Commit `ffa5764` on main with archival message
- [x] Tag `v0.3.0` pushed to origin
- [x] 31/31 tests passing (11 Phase 1 + 20 Phase 2)
- [x] 0 build warnings

### ✅ Compatibility Verified
- [x] No existing public APIs modified
- [x] No execution interfaces changed
- [x] No Phase 1 test expectations altered
- [x] Purely additive — safe to merge forward into Phase 3

---

## Active (Phase 3 — Behavior Graph Runtime)

### 🎯 Key Decisions Needed Before Starting
- [ ] Execution model: step-based vs. tick-based
- [ ] Time budget per execution (hard limit in microseconds?)
- [ ] Node type library: decision nodes, state nodes, action nodes — which first?
- [ ] Graph complexity limits (max depth, max nodes, max edges)
- [ ] How graphs are authored (declarative JSON/XML? custom format?)

### 📋 Implementation Tasks (Pending Decisions)
- [ ] Design deterministic graph executor spec
- [ ] Implement core graph traversal engine
- [ ] Add time limit enforcement per graph execution
- [ ] Add depth limit enforcement
- [ ] Add debug logging and profiling hooks
- [ ] Create state machine validation
- [ ] Write 20+ tests for graph execution
- [ ] Document graph format and limitations
- [ ] Verify all Phase 1 + Phase 2 tests still pass
- [ ] Create v0.4.0 release

---

## Ongoing (All Phases)

### 📚 Documentation Maintenance
- [ ] Keep docs/examples in sync with SDK API
- [ ] Review docs for accuracy on every release
- [ ] Respond to documentation issues in GitHub

### 🧪 Test Coverage
- [ ] Maintain 31+ tests passing
- [ ] Add tests for Phase 3, 4, 5 features as they're implemented
- [ ] Verify zero warnings in Release builds

### 📊 Community Engagement
- [ ] Monitor GitHub Discussions
- [ ] Respond to issues within 48 hours
- [ ] Track real-world use cases and "using in production" confirmations
- [ ] Post Phase 2 release notes on Reddit (r/csharp, r/gamedev)

---

## Planned: Phase 4 (Procedural Control)

- [ ] Design parameter validation schema for procedural inputs
- [ ] Implement seed validation
- [ ] Add generation bounds checking
- [ ] Create v0.5.0 release

---

## Planned: Phase 5 (Cloud Services)

- [ ] Design optional cloud validation service
- [ ] Implement opt-in telemetry (privacy-first)
- [ ] Create v0.6.0+ release

---

## Decision Points

### After Phase 3 (Post-v0.4.0)
**Question:** Does behavior graph support unlock new use cases?

**Signals to look for:**
- Game developers referencing BehaviorGraphs surfaces in Phase 2 registries
- Feature requests for specific node types
- Community-authored example graphs

---

## Backlog: If Adoption Accelerates

1. Improve ModPackager tool (currently stub)
2. Improve SchemaValidator tool (currently stub)
3. Create runnable example end-to-end project
4. Add monetization/sponsorship pathway
5. Phase 3 behavior graph support

---

## Success Criteria (v0.3.0) — ALL MET

- ✅ Zero build errors, zero compiler warnings
- ✅ 31/31 tests passing
- ✅ All public members documented
- ✅ Commit and tag pushed to main
- ✅ No existing runtime behavior changed
- ✅ Phase 3 merge safety verified
