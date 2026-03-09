# BridgeMod Documentation Index

Welcome to the BridgeMod documentation. Start here to find what you're looking for.

---

## 🚀 Getting Started (Start Here)

| Document | Best For | Read Time |
|----------|----------|-----------|
| [SECURITY_ARCHITECTURE.md](SECURITY_ARCHITECTURE.md) | **Understanding the 3-Gate firewall model** — how validation works | 5 min |
| [Phase1_Firewall_Design.md](Phase1_Firewall_Design.md) (if exists) | Deep dive into type checking, boundary guards, audit logging | 15 min |
| [../QUICKSTART.md](../QUICKSTART.md) | Integrating BridgeMod into your game engine in 10 minutes | 10 min |
| [../MOD_SCHEMA.md](../MOD_SCHEMA.md) | Creating mods that work with BridgeMod | 10 min |

---

## 📚 Architecture & Design (Phase By Phase)

### Phase 1: Security Foundation (v0.2.4)
**The 3-Gate firewall: Type Check → Boundary Guards → Audit Logging**

- [SECURITY_ARCHITECTURE.md](SECURITY_ARCHITECTURE.md) — Visual overview of how mods are validated
- Type checking: Schema validation, injection blocking
- Boundary guards: Stat clamping via BridgeConfig
- Audit logging: Append-only tamper-evident event trail

**Status:** ✅ Complete, 11/11 tests passing

---

### Phase 2: Developer Mod Surfaces (v0.3.0)
**Governance layer for declaring what's moddable**

- [Phase2_Mod_Surfaces.md](Phase2_Mod_Surfaces.md) — Full specification
- Surface categories: Data, BehaviorGraphs, ProceduralInputs
- Surface status: Enabled, Limited, Disabled, Planned
- ModSurfaceRegistry (host-level, append-only)
- ModSurfaceSummaryGenerator (auto-generated capability matrix)

**Status:** ✅ Complete, 20/20 tests passing

---

### Phase 3: Behavior Graph Runtime (v0.4.0)
**Deterministic state machines for game logic**

- [Phase3_Runtime.md](Phase3_Runtime.md) — Full architectural specification
- BehaviorGraphDefinition: Immutable graph structure
- BehaviorState & BehaviorTransition: Sealed, deterministic types
- TransitionGuard: 6 guard operators (Equals, NotEquals, GreaterThan, LessThan, GreaterThanOrEqual, LessThanOrEqual)
- BehaviorGraphExecutor: Deterministic event dispatch & state transitions
- Determinism proof: 1000-iteration identical output test

**Status:** ✅ Complete, 15 foundation + 34 runtime = 49/49 tests passing

---

### Phase 4: Procedural Control Layer (v0.5.0)
**Deterministic random number generation & weight-based selection**

- [Phase4_Procedural.md](Phase4_Procedural.md) — Full specification
- BridgeRandom: Xorshift32 PRNG (pure bit-shifting, cross-platform)
- ProceduralWeightTable: Weight normalization with boundary guard integration
- Seed auditing: PROCEDURAL_GEN_001 tracking for reproducibility
- Determinism proof: 1000-iteration seed verification
- Console certification: Why determinism matters

**Status:** ✅ Complete, 15/15 tests passing

---

### Phase 5+: Future (Planning)
**Cloud services, telemetry, player-facing mod browser**

- See [../docs/internal/console_modding_execution_plan.md](internal/console_modding_execution_plan.md) for full roadmap
- Status: Design pending

---

## 🔧 Integration & Examples

| Document | Purpose |
|----------|---------|
| [Engine_Adapter_Model.md](Engine_Adapter_Model.md) | Multi-engine support: how engines integrate BridgeMod |
| [../samples/Legacies_Bridge_Test/Sample_Walkthrough.md](../samples/Legacies_Bridge_Test/Sample_Walkthrough.md) | End-to-end integration example (C# + Python) |

---

## 🏗️ Internal & Planning

| Document | Purpose |
|----------|---------|
| [internal/console_modding_execution_plan.md](internal/console_modding_execution_plan.md) | Full roadmap (Phases 1-5+) and strategic planning |
| [internal/IMPLEMENTATION_STATUS.md](internal/IMPLEMENTATION_STATUS.md) | Current build/test status and architecture |
| [internal/GITHUB_PREP_SUMMARY.md](internal/GITHUB_PREP_SUMMARY.md) | GitHub repository setup & visibility checklist |
| [internal/DISCUSSIONS_STRATEGY.md](internal/DISCUSSIONS_STRATEGY.md) | Community engagement strategy |
| [Experimental_Notes.md](Experimental_Notes.md) | Why schema registry is non-canonical reference implementation |

---

## 🐍 DreamCraft.Engine (Python Simulation Core)

| Document | Purpose |
|----------|---------|
| [DreamCraft_Engine_Architecture.md](DreamCraft_Engine_Architecture.md) | Python engine design & pulse simulation |
| [DreamCraft_Current_State.md](DreamCraft_Current_State.md) | Current implementation status |
| [DreamCraft_Design_Decisions.md](DreamCraft_Design_Decisions.md) | Why we chose certain patterns |
| [DreamCraft_Tasks.md](DreamCraft_Tasks.md) | Remaining work for engine integration |

---

## 📖 How to Read This Documentation

### If you're a **Game Developer** integrating BridgeMod:
1. Start: [SECURITY_ARCHITECTURE.md](SECURITY_ARCHITECTURE.md) (understand the safety model)
2. Then: [../QUICKSTART.md](../QUICKSTART.md) (add to your game)
3. Reference: [Phase2_Mod_Surfaces.md](Phase2_Mod_Surfaces.md) (declare your surfaces)
4. Deep dive: [Phase3_Runtime.md](Phase3_Runtime.md) (if using behavior graphs)

### If you're a **Modder** creating mods:
1. Start: [../MOD_SCHEMA.md](../MOD_SCHEMA.md) (mod package format)
2. Then: [../QUICKSTART.md](../QUICKSTART.md) (submission guidelines)
3. Examples: [../samples/](../samples/) (working mod examples)

### If you're a **Contributor** or **Security Researcher**:
1. Start: [SECURITY_ARCHITECTURE.md](SECURITY_ARCHITECTURE.md)
2. Then: [Phase1_Firewall_Design.md](Phase1_Firewall_Design.md) (if exists, deep dive)
3. Reference: [Phase3_Runtime.md](Phase3_Runtime.md) (determinism proofs)
4. Reference: [Phase4_Procedural.md](Phase4_Procedural.md) (PRNG design)
5. Strategy: [internal/console_modding_execution_plan.md](internal/console_modding_execution_plan.md)

### If you're **Planning a Port** (PC → Console):
1. Start: [SECURITY_ARCHITECTURE.md](SECURITY_ARCHITECTURE.md)
2. Then: [Phase4_Procedural.md](Phase4_Procedural.md) (determinism guarantee)
3. Reference: [Engine_Adapter_Model.md](Engine_Adapter_Model.md) (multi-platform support)
4. Roadmap: [internal/console_modding_execution_plan.md](internal/console_modding_execution_plan.md)

---

## 🎯 Key Concepts at a Glance

### The 3-Gate Firewall (Phase 1)
```
Raw Mod → Type Check → Boundary Guards → Audit Log → Safe Mod
```
See [SECURITY_ARCHITECTURE.md](SECURITY_ARCHITECTURE.md)

### Developer Mod Surfaces (Phase 2)
```
Host declares surfaces → Mod targets surfaces → ModSurfaceSummaryGenerator produces capability matrix
```
See [Phase2_Mod_Surfaces.md](Phase2_Mod_Surfaces.md)

### Behavior Graph Runtime (Phase 3)
```
State Machine Graph → Event Dispatch → Guard Evaluation → Deterministic Transitions
```
See [Phase3_Runtime.md](Phase3_Runtime.md)

### Procedural Control (Phase 4)
```
Seed + Weights → BridgeRandom + ProceduralWeightTable → Deterministic Sequences
```
See [Phase4_Procedural.md](Phase4_Procedural.md)

---

## 📊 Quality Metrics (v0.5.0)

| Metric | Value |
|--------|-------|
| Build Errors | 0 |
| Compiler Warnings | 0 |
| Test Pass Rate | 100/100 (100%) |
| Public API Documentation | 100% |
| Determinism Proof | ✅ (1000-iteration tests) |
| Dependencies Added | 0 new (Newtonsoft.Json 13.0.3 approved) |

---

## 🔗 Quick Links

- **Main README:** [../README.md](../README.md)
- **CONSTITUTION (Principles):** [../CONSTITUTION.md](../CONSTITUTION.md)
- **CONTRIBUTING (Guidelines):** [../CONTRIBUTING.md](../CONTRIBUTING.md)
- **CODE_OF_CONDUCT:** [../CODE_OF_CONDUCT.md](../CODE_OF_CONDUCT.md)
- **Roadmap:** [internal/console_modding_execution_plan.md](internal/console_modding_execution_plan.md)
- **GitHub:** [https://github.com/rootedresilientshop-pixel/BridgeMod](https://github.com/rootedresilientshop-pixel/BridgeMod)
- **NuGet:** [https://www.nuget.org/packages/BridgeMod.SDK/](https://www.nuget.org/packages/BridgeMod.SDK/)

---

## ❓ Can't Find What You're Looking For?

- **General questions?** Check [../CONSTITUTION.md](../CONSTITUTION.md) — it explains our philosophy
- **API questions?** See the appropriate phase doc (Phase 1-4) above
- **Security concerns?** Start with [SECURITY_ARCHITECTURE.md](SECURITY_ARCHITECTURE.md)
- **Community questions?** Open a discussion on [GitHub Discussions](https://github.com/rootedresilientshop-pixel/BridgeMod/discussions)

---

**Last Updated:** March 9, 2026
**Status:** v0.5.0 (Phase 4 Complete)
**License:** MIT
