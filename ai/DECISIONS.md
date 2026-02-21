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

## Phase 3 Decisions (v0.4.0 — Behavior Graph Runtime)

### Deterministic State Machine Architecture

**Decision:** Implement a minimal, deterministic state machine executor with no scripting, reflection, async, or I/O.

**Architecture:**
- **BehaviorGraphDefinition** — immutable graph with states and transitions
- **BehaviorState** — sealed nodes with optional metadata
- **BehaviorTransition** — sealed edges with optional guard conditions
- **TransitionGuard** — primitive-type comparison operators (Equals, NotEquals, GreaterThan, LessThan, GreaterThanOrEqual, LessThanOrEqual)
- **BehaviorGraphExecutor** — stateful executor with event dispatch and guard evaluation
- **BehaviorGraphValidator** — pre-execution validation (duplicates, references, ambiguity)

**Rationale:** Determinism is non-negotiable for console certification. By restricting graphs to primitive types, no reflection, and no dynamic loading, we guarantee that the same input always produces the same output. This enables safe replays, testing, and console ports.

### Guard Operator Design

**Decision:** Support only 6 primitive-type operators. No complex object comparisons, no string interpolation, no expression evaluation.

**Supported Operators:**
- Equals, NotEquals (all primitives: int, float, bool, string)
- GreaterThan, LessThan, GreaterThanOrEqual, LessThanOrEqual (numeric only)

**Rationale:** Primitive types and simple comparisons are deterministic. Complex object comparisons require reflection or dynamic dispatch, which breaks reproducibility. If games need complex logic, they can layer it on top of the graph executor.

### No Scripting (Explicit Design Decision)

**Decision:** Behavior graphs are declarative node structures. No C#, Python, Lua, or any other scripting language is supported.

**Rationale:** Scripting requires dynamic code loading, reflection, or unsafe execution. These are unsafe for console certification and break the determinism guarantee. Games need transparent, auditable logic — behavior graphs provide that at the cost of not allowing arbitrary code.

**Implication:** All game logic must be either:
1. Pure data (JSON configs in Phase 1)
2. Behavior graphs (Phase 3)
3. Procedural parameters (Phase 4)
4. Custom game code (outside BridgeMod scope)

### Pre-Execution Validation

**Decision:** `BehaviorGraphValidator` catches graph errors before execution, not at runtime.

**Validation Rules:**
- No duplicate state IDs
- Initial state exists
- All transition references are valid
- Guard operators are well-formed
- No ambiguous transitions (at most one transition per state+event pair)

**Rationale:** Catching errors early prevents runtime failures and makes debugging easier. The host can validate graphs at mod load time and reject invalid mods before they affect gameplay.

### Single-Threaded, Synchronous Execution

**Decision:** Executor transitions are immediate and single-threaded. No async, no threading, no background execution.

**Rationale:** Multiplayer and concurrent execution introduce race conditions and non-determinism. By keeping execution synchronous and single-threaded, we preserve the determinism guarantee and make it easier to understand how graphs behave.

### Immutability of Graph Definitions

**Decision:** `BehaviorGraphDefinition` is sealed and immutable. Graph structure cannot change at runtime.

**Rationale:** Immutable graphs are safe to share across multiple executors and threads. They can be validated once, cached, and reused without fear of mutation.

**Implication:** If a graph needs to evolve, create a new definition and migrate executors to the new version. This is a host responsibility, not an SDK responsibility.

### No Execution Time Limits (Phase 3)

**Decision:** Phase 3 does not enforce execution time limits. Graph execution is synchronous and immediate, so the host can set its own timeout at the dispatch level if needed.

**Rationale:** Time budgets are game-specific. Some games may allow long-running graph traversals; others may need strict microsecond budgets. Phase 3 provides the mechanism; the host controls the policy.

**Future:** Phase 4+ may introduce optional execution constraints for nested graphs or complex procedural systems.

### Forward Compatibility: No Type Removals

**Decision:** Once a type is in the public API, it never changes. New features are additive.

**Rationale:** Engines implement adapters around the BridgeMod contract. If BridgeMod changes the contract, all engines break. By committing to additive-only changes, we guarantee that existing engines continue to work indefinitely.

**Implication:** If we need to change a type, we deprecate it and introduce a new version. Old versions remain supported in parallel.

### No Experimental Behavior Graphs in Main Branch

**Decision:** The `experimental/dreamcraft-introspection` branch contains schema registry tooling (non-canonical reference material). This tooling does not ship on `main` and is explicitly non-authoritative.

**Rationale:** The canonical contract lives in the C# SDK. Experimental tooling is optional, reference material. Keeping it on a separate branch prevents confusion and avoids burdening the main build with non-essential code.

---

## Unresolved Decisions (For Future Phases)

1. **Phase 3 Nested Graphs:** Should Phase 4 support graphs containing graphs? (Deferred to Phase 4)
2. **Phase 3 Weighted Transitions:** Should Phase 4 support probabilistic transitions? (Deferred to Phase 4)
3. **Phase 4 Procedural:** How strict are bounds checking? (Planned: very strict, fail-safe)
4. **Phase 4 Seeds:** Should procedural systems support deterministic seeding? (Planned: yes)
5. **Phase 5 Cloud:** Optional or default? (Planned: optional, local-first)
6. **Monetization:** When/how to monetize (pending developer feedback)
7. **Tool Projects:** When/how to implement `ModPackager` and `SchemaValidator` (pending Phase 4+ feature clarity)
8. **AuditLogger File Export:** Deferred from Phase 2 — when to implement? (Candidate for Phase 4 or standalone patch)
