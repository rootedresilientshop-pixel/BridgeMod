# Decisions

This file documents key architectural and process decisions across all phases.

---

## Phase 1 Decisions (v0.2.4 — Firewall Standard)

### Product/Architecture

- Mods are package-based (`.zip`) with required `manifest.json`
- Runtime prioritizes local validation before load (no cloud prerequisite)
- Mods treated as untrusted input with failure containment and disable-on-error behavior
- Public API exposes explicit mod surface declaration by game developers
- Supported mod categories: Data, BehaviorGraph, Procedural
- Schema-first validation (not inferred shape acceptance)
- Data mods are sandbox-safe for console deployment

### Security Architecture: "Firewall Standard"

**Decision:** Implement a 3-gate C# validation pipeline as the core SDK architecture.

**Pipeline:**
1. **Type Check** — Validates input shape and types
2. **Boundary Guards** — Configurable via `BridgeConfig`; clamps out-of-bounds values
3. **Audit Logging** — Records all validation decisions for traceability

**Rationale:** A single, predictable pipeline is easier to certify for console compliance and easier to reason about than ad-hoc validation scattered across the codebase.

### Air-Gap Strategy

**Decision:** The C# SDK validates all data before it reaches the Python simulation engine. The Python Engine only consumes "scrubbed" JSON.

**Rationale:** Isolates the two runtimes. Even if a mod contains malicious input, the Python Engine never sees raw mod data. The Engine falls back to safe defaults if the SDK blocks a mod, preventing crashes.

**Implication:** Two-layer architecture is the required pattern. Any future engine integrations must follow the same scrubbed-input contract.

### Framework Support

**Decision:** SDK targets `net8.0` + `netstandard2.1` for broadest Unity/Mono compatibility. Tests target `net10.0`.

**Rationale:** `netstandard2.1` ensures Unity and Mono compatibility. `net8.0` is the current LTS. Tests run on `net10.0` to stay current with the CI environment.

### Repository Layout

**Decision:** Professional `/src/` + `/samples/` layout. `/src/BridgeMod.SDK` is the single source of truth.

**New Structure:**
- `/src/BridgeMod.SDK` — Authoritative C# library
- `/src/DreamCraft.Engine` — Isolated Python simulation core
- `/samples/Legacies_Bridge_Test` — End-to-end integration proof

### Technical Choices

- JSON stack: `Newtonsoft.Json 13.0.3`
- Unit testing: xUnit
- All public members have XML documentation (`GenerateDocumentationFile=true`)
- Nullable reference warnings eliminated across SDK

### Documentation Strategy

**Decision:** Remove phantom API references, verify all examples against live SDK surface.

**Rationale:** Docs should describe what actually exists. Phantom references undermine trust and cause developer frustration.

### Branding

**Decision:** Establish "Firewall Standard" as BridgeMod's unique selling point for console-compliant modding.

**Rationale:** Console publishers need auditable, sandbox-safe, offline-capable mod systems. "Firewall Standard" is a memorable, specific claim that differentiates BridgeMod from generic mod SDKs.

---

## Phase 2 Decisions (v0.3.0 — Developer Mod Surfaces)

### Declarative Governance Layer (Not Runtime)

**Decision:** Phase 2 is metadata-only. No runtime enforcement, no execution pipeline integration, no serialization changes, no reflection.

**Rationale:** The purpose of Phase 2 is transparency — giving developers a formal mechanism to declare what is moddable and allowing tooling to generate capability matrices. Any runtime enforcement would couple the governance layer to the execution pipeline, violating the architectural isolation principle.

**Implication:** Surface status values (`Enabled`, `Limited`, `Disabled`, `Planned`) are declarations. Enforcement is the host's responsibility. The SDK will never automatically block a mod based on surface status.

### Immutability of `ModSurfaceDeclaration`

**Decision:** `ModSurfaceDeclaration` is sealed and immutable. All properties are constructor-assigned with no setters. No mutation methods.

**Rationale:** Surface declarations are facts about game design, not runtime state. Making them immutable prevents accidental mutation after registration and makes the registry a reliable source of truth for capability matrix generation.

### Append-Only Registry

**Decision:** `ModSurfaceRegistry` supports registration only — no removal, no modification of existing entries.

**Rationale:** Removing a surface declaration after it has been published (e.g., in a generated `MOD_SURFACES.md`) would create a misleading capability matrix. The append-only contract ensures that registered surfaces are stable for the lifetime of a game session.

**Initialization-only contract:** The registry must be populated before gameplay begins. The SDK does not enforce this — it is an architectural responsibility of the host.

### Case-Sensitive Duplicate Prevention

**Decision:** `ModSurfaceRegistry` uses ordinal (case-sensitive) comparison for duplicate detection.

**Rationale:** Surface names are developer-authored identifiers. Case sensitivity prevents accidental near-duplicates (`WeaponBalance` vs `weaponbalance`) while remaining predictable and consistent with C# identifier conventions.

### Pure String Output from Generator

**Decision:** `ModSurfaceSummaryGenerator.Generate()` returns a string. It performs no I/O, writes no files, and outputs nothing to the console.

**Rationale:** The generator's responsibility is content production, not delivery. The host decides how to use the output — write to `MOD_SURFACES.md`, serve via API, display in-game, log at startup, or diff in CI. Separating generation from delivery keeps the generator testable, side-effect-free, and flexible.

### Alphabetical Ordering in Capability Matrix

**Decision:** Categories are sorted alphabetically; surfaces within each category are sorted alphabetically by name.

**Rationale:** Deterministic output order makes the capability matrix stable across SDK versions and host environments. Alphabetical order is predictable to both humans and tooling (e.g., `diff` on two matrices will only show meaningful changes).

### No Modification of Existing APIs

**Decision:** Phase 2 adds only new types. No existing classes, interfaces, constructors, or method signatures were modified.

**Rationale:** Phase 2 must be safely mergeable into Phase 3 and beyond without breaking existing consumers. Zero modifications to existing public APIs is the strongest possible forward-compatibility guarantee.

### Exclusion of `tests/Automation/` from Main Branch

**Decision:** `tests/BridgeMod.Tests.csproj` excludes `Automation/**/*.cs` via a `<Compile Remove>` directive.

**Rationale:** The `tests/Automation/` directory is an untracked artifact from the `dev/phase-2-automation` branch. It references `BridgeMod.Generator` which does not exist on `main`. The exclusion allows the working tree to contain these files without breaking compilation on `main`.

---

## Unresolved Decisions (For Future Phases)

1. **Phase 3 Behavior Graphs:** Execution model (step-based vs. tick-based)? Time budget per execution? Node type library priority order? Graph authoring format?
2. **Phase 3 Behavior Graphs:** Will BridgeMod provide the graph executor, or expect games to implement? (Planned: provide executor)
3. **Phase 4 Procedural:** How strict are bounds checking? (Planned: very strict, fail-safe)
4. **Phase 5 Cloud:** Optional or default? (Planned: optional, local-first)
5. **Monetization:** When/how to monetize (pending developer feedback)
6. **Tool Projects:** When/how to implement `ModPackager` and `SchemaValidator` (pending Phase 3+ feature clarity)
7. **AuditLogger File Export:** Deferred from Phase 2 — when to implement? (Candidate for Phase 3 or standalone patch)
