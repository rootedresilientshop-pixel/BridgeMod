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

## ✅ Phase 3: Behavior Graph Runtime — COMPLETE (v0.4.0)

### ✅ Core Types Implemented
- [x] `BehaviorGraphDefinition` — sealed, immutable graph structure (states, transitions, initial state)
- [x] `BehaviorState` — sealed immutable state nodes with optional display name and metadata
- [x] `BehaviorTransition` — sealed transitions with optional guard conditions
- [x] `TransitionGuard` — guard operators: Equals, NotEquals, GreaterThan, LessThan, GreaterThanOrEqual, LessThanOrEqual
- [x] `BehaviorGraphValidator` — pre-execution validation (no duplicates, valid references, no ambiguous transitions)
- [x] `BehaviorGraphExecutor` — deterministic state machine with guard evaluation and event dispatch
- [x] `GuardOperator` — enum with 6 deterministic operators for primitive type comparison

### ✅ Determinism Guarantees Implemented
- [x] Same input → Same output, always (no randomness, reflection, async, or I/O)
- [x] Single-threaded, synchronous state transitions
- [x] Guard operators support only primitives (int, float, bool, string)
- [x] Pre-execution validation catches errors before runtime
- [x] 1000-iteration determinism proof test verifies identical output from identical input

### ✅ Tests (34 new tests)
- [x] `tests/ModBehaviorGraphTests.cs` — 34 tests covering:
  - State creation and validation
  - Transition creation and validation
  - Guard creation with all 6 operators
  - Guard evaluation with primitive types
  - Graph definition validation
  - Validator tests (duplicates, references, ambiguity)
  - Executor initialization and transitions
  - No-match behavior (executor stays in place)
  - Guard condition failure handling
  - Determinism proof (1000 iterations with identical results)

### ✅ Documentation
- [x] README.md — Phase 3 section: core concepts, determinism guarantee, guard operators table, enemy AI example, integration guidance
- [x] `docs/Phase3_Runtime.md` — full architectural specification (6600+ words)
  - Architecture diagram
  - Core types and contracts
  - Determinism guarantees and proof
  - Guard operators reference
  - Integration examples (enemy AI, dialogue trees)
  - JSON representation format
  - Forward compatibility strategy
  - Limitations (intentional: no scripting, no reflection, no async)

### ✅ Release
- [x] SDK version bumped to 0.4.0 in `BridgeMod.SDK.csproj`
- [x] Commits `fd88330` (Phase 3 implementation) + `3f3cc1d` (version bump) on main
- [x] Tag `v0.4.0` pushed to GitHub origin
- [x] Published to NuGet.org (live)
- [x] 80/80 tests passing (11 Phase 1 + 20 Phase 2 + 15 Phase 3 Foundation + 34 Phase 3 Runtime)
- [x] 0 build warnings in Release configuration
- [x] STATE.md updated with Phase 3 completion

### ✅ Compatibility Verified
- [x] No existing public APIs modified
- [x] No Phase 1 or Phase 2 behavior changed
- [x] All 80 tests passing (comprehensive regression)
- [x] Purely additive — safe to merge forward into Phase 4

---

## ✅ AuditLogger File Export + Stuck State Detector — COMPLETE (v0.4.1)

### ✅ Core Changes
- [x] Add `Newtonsoft.Json 13.0.3` to `BridgeMod.SDK.csproj`
- [x] Add `WarnStuck001 = "WARN_STUCK_001"` to `ErrorCodes` in `BridgeMod.Bridge.cs`
- [x] Add thread-safety lock (`_lock` object) to `AuditLogger.Log`, `HasCode`, and new `FlushToDisk`
- [x] Implement `AuditLogger.FlushToDisk(string path)` — snapshot under lock, JSON array via Newtonsoft.Json, no-throw
- [x] Add optional `AuditLogger? auditLogger = null` parameter to `BehaviorGraphExecutor` constructor
- [x] Log `WarnStuck001` in `Dispatch` when no transition matches (backward compatible)

### ✅ Tests (5 new tests in `tests/AuditLoggerTests.cs`)
- [x] `FlushToDisk_WritesJsonFile_ContainingAllEntries`
- [x] `FlushToDisk_IsNoThrow_WhenPathIsInvalid`
- [x] `FlushToDisk_ProducesEmptyArray_WhenNoEntriesLogged`
- [x] `StuckState_LogsWarnStuck001_WhenNoTransitionMatches`
- [x] `StuckState_DoesNotLog_WhenTransitionSucceeds`

### ✅ Documentation
- [x] README.md — Fixed 3 Phase 3 API inaccuracies (Validate return type, Initialize() removal, Dispatch void return)
- [x] README.md — Added Premium Studio Tools section
- [x] `ai/STATE.md` — Removed AuditLogger in-memory warning, added completion note, bumped to v0.4.1
- [x] `ai/TASKS.md` — Added this task section

### ✅ Release
- [x] SDK version bumped to 0.4.1 in `BridgeMod.SDK.csproj`
- [x] 85/85 tests passing (11 Phase 1 + 20 Phase 2 + 15 Phase 3 Foundation + 34 Phase 3 Runtime + 5 AuditLogger/StuckState)
- [x] 0 build warnings in Release configuration
- [x] All Phase 1–3 tests still passing (no regressions)
- [x] Backward compatible: existing `BehaviorGraphExecutor(definition)` callers unaffected

---

## Ongoing (All Phases)

### 📚 Documentation Maintenance
- [ ] Keep docs/examples in sync with SDK API
- [ ] Review docs for accuracy on every release
- [ ] Respond to documentation issues in GitHub

### 🧪 Test Coverage
- [x] Maintain 85/85 tests passing (11 Phase 1 + 20 Phase 2 + 15 Phase 3 Foundation + 34 Phase 3 Runtime + 5 AuditLogger/StuckState)
- [ ] Add tests for Phase 4, 5 features as they're implemented
- [x] Verify zero warnings in Release builds

### 📊 Community Engagement
- [ ] Monitor GitHub Discussions
- [ ] Respond to issues within 48 hours
- [ ] Track real-world use cases and "using in production" confirmations
- [ ] Post Phase 2 release notes on Reddit (r/csharp, r/gamedev)

---

## Planned: Phase 4 (Procedural Control Layer) — v0.5.0

### Scope
- [ ] Design parameter validation schema for procedural inputs
- [ ] Implement seed validation and generation parameter bounds
- [ ] ProceduralInputs surface category runtime execution
- [ ] Guard conditions for procedural systems
- [ ] Pre-execution validation for procedural graphs
- [ ] 20+ tests for procedural executor
- [ ] Full documentation with examples

### Quality Gates
- [ ] 100+ total tests passing
- [ ] 0 build warnings
- [ ] All Phase 1-3 tests still passing (no regressions)
- [ ] 100% public API documented
- [ ] Determinism guarantee preserved

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

## Success Criteria (v0.4.0) — ALL MET

- ✅ Zero build errors, zero compiler warnings (Release)
- ✅ 80/80 tests passing (11 Phase 1 + 20 Phase 2 + 15 Phase 3 Foundation + 34 Phase 3 Runtime)
- ✅ All public members documented
- ✅ Commits `fd88330` + `3f3cc1d` pushed to main; tag `v0.4.0` pushed to origin
- ✅ Published to NuGet.org (live)
- ✅ No existing runtime behavior changed
- ✅ Determinism guarantee proven and tested (1000-iteration proof)
- ✅ Phase 4 merge safety verified

## Success Criteria (v0.4.1) — ALL MET

- ✅ Zero build errors, zero compiler warnings (Release)
- ✅ 85/85 tests passing (11 Phase 1 + 20 Phase 2 + 15 Phase 3 Foundation + 34 Phase 3 Runtime + 5 AuditLogger/StuckState)
- ✅ All public members documented
- ✅ No existing runtime behavior changed
- ✅ Backward compatible: all 80 pre-existing tests pass unmodified
- ✅ No-throw guarantee on FlushToDisk verified by test
- ✅ Thread-safe lock pattern applied to AuditLogger
- ✅ Premium Studio Tools section added to README.md with MIT license clarification
