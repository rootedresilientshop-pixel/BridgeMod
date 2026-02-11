# Console‑Tolerant Modding Platform — Execution Plan (v1)

## Purpose
This document is a **Claude Code–ready execution plan** for building a **developer‑first, console‑tolerant modding platform**, starting with **Unity + Xbox**, designed to narrow the PC–console modding gap without violating platform constraints.

The plan is written to be:
- Executable step‑by‑step by Claude Code inside VS Code
- Credible to professional game developers
- Tolerable within the Xbox ecosystem
- Expandable to PC parity and future crowdfunding goals

---

## Core Principles (Non‑Negotiable)

1. **Developer‑Authoritative Capabilities**
   - Developers decide what mod surfaces exist.
   - Platform never overrides developer limits.

2. **Platform‑Authoritative Transparency**
   - Mod surfaces are declared, visible, and comparable.
   - Community pressure is social, not technical.

3. **Data & Graph‑Driven Mods Only (v1)**
   - No runtime scripting.
   - No executable code from mods.
   - Mods are treated as untrusted data.

4. **Hybrid Validation**
   - Local validation is mandatory and sufficient.
   - Cloud validation is optional and additive.

5. **Sandboxed Failure Containment**
   - Mods may fail.
   - Games may not.

6. **Anonymous, File‑Based Distribution (v1)**
   - No accounts required.
   - No moderation burden.
   - Identity deferred to later phases.

---

## v1 Mod Capability Scope

### Supported
- Pure data mods (configs, balance, definitions)
- Behavior definition via graphs (state machines, ECA rules)
- Procedural control (seeds, generation parameters)
- PC parity as a design goal

### Deferred (v2+)
- Asset replacement
- Sandboxed scripting
- Player‑facing mod browsers
- Account systems

---

## High‑Level Architecture

```
┌────────────┐
│ Unity Game │
│ (Developer)│
└─────┬──────┘
      │
      ▼
┌─────────────────────┐
│ Mod Runtime SDK     │
│ - Loader            │
│ - Validator         │
│ - Graph Executor    │
│ - Failure Sandbox   │
└─────┬───────────────┘
      │
      ▼
┌─────────────────────┐
│ Optional Cloud      │
│ Services            │
│ - Schema checks     │
│ - Compatibility     │
│ - Metadata          │
└─────────────────────┘
```

---

## Repository Structure

```
mod-platform/
├─ docs/
│  ├─ vision.md
│  ├─ mod-surfaces.md
│  ├─ xbox-compliance.md
│
├─ sdk/
│  ├─ Runtime/
│  │  ├─ ModLoader.cs
│  │  ├─ ModValidator.cs
│  │  ├─ ModSandbox.cs
│  │  └─ ExecutionGuards.cs
│  │
│  ├─ Data/
│  │  ├─ Schemas/
│  │  └─ Versioning/
│  │
│  ├─ BehaviorGraphs/
│  │  ├─ GraphModel.cs
│  │  ├─ GraphExecutor.cs
│  │  └─ GraphValidation.cs
│  │
│  └─ PublicAPI/
│     └─ ModSurfaceDeclaration.cs
│
├─ tools/
│  ├─ ModPackager/
│  ├─ SchemaValidatorCLI/
│
├─ cloud/
│  ├─ validation-service/
│  └─ metadata-service/
│
└─ examples/
   └─ UnitySampleGame/
```

---

## Execution Phases

### PHASE 1 — Foundations
**Goal:** Safe mod loading without cloud dependency.

Claude Tasks:
1. Define mod package format (manifest + payload).
2. Implement local mod loader with strict schema checks.
3. Implement versioning and compatibility rules.
4. Add execution guards and automatic disable on error.

Deliverable:
- Unity SDK that loads data‑only mods safely.

---

### PHASE 2 — Developer Mod Surfaces
**Goal:** Give developers explicit control.

Claude Tasks:
1. Implement `ModSurfaceDeclaration`.
2. Support categories:
   - Data
   - Behavior graphs
   - Procedural inputs
3. Generate human‑readable surface summaries.
4. Expose tooling for devs to mark surfaces as:
   - Enabled
   - Limited
   - Disabled
   - Planned

Deliverable:
- Transparent mod capability matrix per game.

---

### PHASE 3 — Behavior Graph Runtime
**Goal:** Close the PC–console modding gap meaningfully.

Claude Tasks:
1. Define graph schema (nodes, edges, constraints).
2. Build deterministic graph executor.
3. Add time and depth limits.
4. Integrate with Unity update loop safely.
5. Implement debug logging and failure isolation.

Deliverable:
- Console‑safe behavioral modding.

---

### PHASE 4 — Procedural Control Layer
**Goal:** Replayability and creative power without risk.

Claude Tasks:
1. Expose seed and parameter interfaces.
2. Enforce determinism.
3. Validate parameter bounds.
4. Document best practices for devs.

Deliverable:
- Procedural mod support that survives certification.

---

### PHASE 5 — Optional Cloud Validation (Deferred Start)
**Goal:** Platform leverage without dependency.

Claude Tasks:
1. Design validation API contract.
2. Implement schema compatibility checks.
3. Return warnings, never blocks.
4. Ensure SDK functions fully offline.

Deliverable:
- Optional quality layer, crowdfunding‑ready.

---

## Xbox Compliance Narrative (Key Points)

- Mods are data, not code.
- No runtime code injection.
- No filesystem access outside sandbox.
- No unsigned executables.
- Mods validated locally by game.
- Cloud services are advisory only.

---

## Claude Code Instructions

Claude should:
- Never introduce runtime scripting in v1.
- Treat mods as hostile input.
- Prefer deterministic execution.
- Favor explicit schemas over inference.
- Log failures clearly and disable mods automatically.

Claude must not:
- Add networking to runtime
- Add file IO beyond declared mod paths
- Assume account systems
- Assume platform endorsement

---

## v2 / v3 Expansion Hooks

Planned but excluded:
- Asset pipelines
- Lua‑like sandbox
- Player mod discovery
- Account‑based services
- Unreal Engine support

These must be additive, not breaking.

---

## Definition of Success (Early)

- One Unity game ships with the SDK.
- Mods run on Xbox without crashes.
- Developers feel safe enabling mod surfaces.
- Modders feel informed, not boxed out.
- Platform is tolerated, even if not endorsed.

---

## Closing Note

This platform does not force openness.
It **rewards it**, documents it, and lets the community decide what to celebrate.

Build carefully.
Build credibly.
Build something that can survive contact with reality.
