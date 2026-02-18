# Project: BridgeMod

BridgeMod is a C#/.NET SDK for developer-controlled, safety-first game mod loading.

## Identity

- **Name:** BridgeMod
- **Domain:** Game modding SDK/runtime + documentation + tooling
- **Language/Runtime:** C# on .NET 10.0 (tests); SDK targets net8.0 + netstandard2.1
- **License:** MIT
- **Current Version:** v0.3.0 (Phase 2 — Developer Mod Surfaces) — LIVE on GitHub
- **Repository:** https://github.com/rootedresilientshop-pixel/BridgeMod
- **NuGet:** https://www.nuget.org/packages/BridgeMod.SDK/

## Goals (Supported by Current Implementation)

- ✅ Load ZIP-based mods with `manifest.json` metadata
- ✅ Validate mod packages locally before loading (no cloud dependency)
- ✅ Treat mods as untrusted input with automatic isolation on failure
- ✅ Let game developers declare allowed mod surfaces for modders
- ✅ Provide comprehensive docs/examples for developer integration
- ✅ Support console/multi-platform deployment (Xbox, PlayStation, etc.)
- ✅ Generate auto-documented mod surfaces for modders (capability matrix)

## Non-Goals (Current and Future Phases)

- ❌ No runtime scripting execution (v1 data-driven only; behavior graphs in Phase 3+)
- ❌ No asset replacement pipeline (future phase)
- ❌ No player-facing mod browser (Phase 5)
- ❌ No required cloud backend (local validation only; optional cloud in Phase 5)

## Phase Roadmap

| Phase | Status | Focus | Version |
|-------|--------|-------|---------|
| **Phase 1** | ✅ Complete | Security Foundation: "Firewall Standard" — 3-gate C# validation pipeline, Audit Logging, Air-Gap architecture | v0.2.4 |
| **Phase 2** | ✅ Complete | Developer Mod Surfaces: declarative surface registry, capability matrix generation, transparency tooling | v0.3.0 |
| **Phase 3** | 🔄 Planned | Behavior graph runtime (state machines, AI) | v0.4.0 |
| **Phase 4** | 📋 Planned | Procedural control layer | v0.5.0 |
| **Phase 5** | 📋 Planned | Cloud validation services | v0.6.0+ |

## Key Constraints & Properties

- **Safety-First:** Validation, bounded behavior, disable-on-error are non-negotiable
- **Platform-Agnostic:** Same code runs on PC, console, cloud gaming, mobile
- **Offline-Capable:** Full functionality without cloud dependency
- **Data-Driven:** v0.3.0 supports pure data mods (scripts come in Phase 3+)
- **Developer Control:** Game devs explicitly declare what's moddable
- **Purely Additive Phases:** Each phase extends the SDK without breaking existing consumers
- **Production-Ready:** v0.3.0 ships with zero compiler warnings, 31/31 tests passing

## What v0.3.0 Delivered (Phase 2 — Developer Mod Surfaces)

### New Components
- ✅ `ModSurfaceCategory` — enum: Data, BehaviorGraphs, ProceduralInputs
- ✅ `ModSurfaceStatus` — enum: Enabled, Limited, Disabled, Planned
- ✅ `ModSurfaceDeclaration` — sealed, immutable surface record (no setters, no runtime hooks)
- ✅ `ModSurfaceRegistry` — append-only host-level registry; duplicate prevention; IReadOnlyList exposure
- ✅ `ModSurfaceSummaryGenerator` — pure string generator producing grouped, alphabetically-ordered capability matrices

### Governance Properties
- ✅ Host declares surfaces during initialization — mods have zero access to registry
- ✅ Status values are governance metadata — no runtime enforcement by the SDK
- ✅ Capability matrix output is pure string — caller decides how to publish it
- ✅ No runtime execution changes — Phase 1 firewall is 100% unmodified

### Quality Metrics
- ✅ 0 build errors, 0 compiler warnings (Release)
- ✅ 31/31 tests passing (11 Phase 1 + 20 Phase 2)
- ✅ 100% public API documented
- ✅ Commit `ffa5764` on main; tag `v0.3.0` pushed

## What v0.2.4 Delivered (Phase 1 — Firewall Standard)

### Architecture
- ✅ "Firewall Standard" — 3-gate C# validation pipeline: Type Check → Boundary Guards → Audit Logging
- ✅ "Air-Gap" security: C# SDK validates data; Python Engine only consumes scrubbed JSON
- ✅ Python Engine falls back to safe defaults if the SDK blocks a mod
- ✅ Malicious input (script injection) blocked without crashing the Python engine
- ✅ Out-of-bounds stats clamped via configurable BridgeConfig boundary guards
- ✅ Audit logging operational

## Stakeholders & Audiences

1. **Game Developers** (Primary)
   - Need: Safe, simple mod system without crashes
   - Solve: One NuGet package, 10 minutes to integrate

2. **Modders** (Secondary)
   - Need: Clear documentation on what's moddable
   - Solve: Auto-generated capability matrix per game

3. **Console Publishers** (Growing)
   - Need: Safe, auditable, offline-capable mod support
   - Solve: Data-only + sandbox + no arbitrary code execution

4. **Game Studios** (Future)
   - Need: Multi-platform mod support (PC + console parity)
   - Solve: Same code everywhere, platform abstraction

## Branding

**"Firewall Standard"** is BridgeMod's unique selling point for console-compliant modding. The 3-gate pipeline (Type Check → Boundary Guards → Audit Logging) is the core identity differentiator.

## Next Major Milestone

**v0.4.0 (Phase 3 — Behavior Graph Runtime):** Deterministic state machine and ECA rule graph execution. No scripting — declarative node graphs only. Preserves all deterministic guarantees from Phases 1 and 2.
