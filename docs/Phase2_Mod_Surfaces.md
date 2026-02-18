# Phase 2 — Developer Mod Surfaces
## Architectural Reference

**BridgeMod.SDK v0.3.0**
**Status: Complete**
**Branch: main**

---

## Design Philosophy

Phase 2 is a structural maturity release. It does not change what BridgeMod *does* — it formalizes what BridgeMod *communicates*.

The guiding principle is **governance through transparency**. A game developer who ships BridgeMod has a responsibility to their modding community: to say clearly, for each part of their game, whether it is open for modding, restricted, closed, or forthcoming. Without a formal mechanism for this, the information lives in wikis, forum posts, and Discord messages — fragmented, stale, and inaccessible to tooling.

Phase 2 introduces that mechanism. It is purely declarative. No executable behavior is introduced. No runtime logic is changed.

---

## Why Surfaces Exist

A "mod surface" is a named boundary that the game developer exposes to the modding ecosystem. It answers three questions:

1. **What** can be modded here? (Name + Description)
2. **What kind** of data does it accept? (Category)
3. **Is it open right now?** (Status)

Surfaces allow the host to govern mod boundaries at a semantic level, independent of the implementation. A `WeaponBalance` surface means exactly that — regardless of whether the underlying data lives in JSON, a SQLite table, or a binary blob. The surface is the contract; the implementation is the detail.

---

## Components Introduced

### `ModSurfaceCategory` (enum)

Classifies a surface into one of three high-level domains:

| Value | Domain |
|-------|--------|
| `Data` | JSON configs, stat tables, balance sheets, item definitions. No executable content. |
| `BehaviorGraphs` | State machines and ECA rule graphs. Declarative node structures only. No scripting. |
| `ProceduralInputs` | Seeds, weightings, and generation parameters. Numeric and symbolic values. |

Categories drive grouping in the capability matrix generator. They carry no runtime semantics.

### `ModSurfaceStatus` (enum)

Expresses the host-controlled availability of a surface:

| Value | Meaning |
|-------|---------|
| `Enabled` | Fully open for modding within its declared constraints. |
| `Limited` | Available with reduced scope or additional restrictions. |
| `Disabled` | Declared but not open. Retained for transparency. |
| `Planned` | On the roadmap. Declared early to signal developer intent. |

Status values are governance metadata. They do not alter validation logic, execution pipelines, or runtime behavior. The host decides how to act on them.

### `ModSurfaceDeclaration` (sealed class)

An immutable record of a single mod surface. All properties are assigned at construction and cannot be modified thereafter. There are no setters, no mutation methods, and no runtime callbacks.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Unique identifier for the surface within its registry. Non-null, non-whitespace. |
| `Category` | `ModSurfaceCategory` | Domain category. |
| `Status` | `ModSurfaceStatus` | Current availability status. |
| `Description` | `string` | Human-readable explanation. Non-null, non-whitespace. |

**Constructor guards:**
- `ArgumentException` if `Name` is null or whitespace.
- `ArgumentException` if `Description` is null or whitespace.

### `ModSurfaceRegistry` (sealed class)

A host-level accumulator for `ModSurfaceDeclaration` instances. Surfaces are registered once during the host's initialization phase and are never removed.

**Contract:**
- `Register(declaration)` — adds a surface. Throws `ArgumentNullException` for null input. Throws `InvalidOperationException` for duplicate names (case-sensitive).
- `Surfaces` — exposes registered surfaces as `IReadOnlyList<ModSurfaceDeclaration>` in insertion order.
- No removal API exists by design.

> **Initialization-only contract:** This registry must be populated during the host's initialization phase, before gameplay begins. The registry provides no runtime enforcement of this contract — it is the host's architectural responsibility.

### `ModSurfaceSummaryGenerator` (static class)

Accepts a `ModSurfaceRegistry` and returns a formatted capability matrix string. All operations are pure — no I/O, no console output, no file writing, no network access.

**Generation rules:**
1. Categories are sorted alphabetically.
2. Surfaces within each category are sorted alphabetically by name.
3. Each surface entry shows: name, status in brackets, and description indented below.

---

## How Developers Use Surfaces

### 1. Register surfaces at initialization

```csharp
var registry = new ModSurfaceRegistry();

registry.Register(new ModSurfaceDeclaration(
    name: "WeaponBalance",
    category: ModSurfaceCategory.Data,
    status: ModSurfaceStatus.Enabled,
    description: "Base damage and scaling factors for all weapon types."));

registry.Register(new ModSurfaceDeclaration(
    name: "EnemyAI",
    category: ModSurfaceCategory.BehaviorGraphs,
    status: ModSurfaceStatus.Limited,
    description: "Enemy decision trees. Read-only graph nodes only — no custom nodes."));

registry.Register(new ModSurfaceDeclaration(
    name: "WorldSeed",
    category: ModSurfaceCategory.ProceduralInputs,
    status: ModSurfaceStatus.Disabled,
    description: "Reserved for host use. Seed manipulation is not exposed to mods."));

registry.Register(new ModSurfaceDeclaration(
    name: "QuestRewards",
    category: ModSurfaceCategory.Data,
    status: ModSurfaceStatus.Planned,
    description: "Quest reward tables. Planned for Phase 4."));
```

### 2. Generate a capability matrix

```csharp
string matrix = ModSurfaceSummaryGenerator.Generate(registry, "MyGame");

// matrix now contains:
//
// === MyGame — Mod Capability Matrix ===
//
// [BehaviorGraphs]
//   EnemyAI [Limited]
//     Enemy decision trees. Read-only graph nodes only — no custom nodes.
//
// [Data]
//   QuestRewards [Planned]
//     Quest reward tables. Planned for Phase 4.
//   WeaponBalance [Enabled]
//     Base damage and scaling factors for all weapon types.
//
// [ProceduralInputs]
//   WorldSeed [Disabled]
//     Reserved for host use. Seed manipulation is not exposed to mods.
```

### 3. Publish the matrix

The generator returns a plain string. The host decides how to use it:

- Write it to `MOD_SURFACES.md` in the game's mod directory.
- Serve it via an in-game documentation screen.
- Include it in a developer portal or API response.
- Log it at startup for audit purposes.

---

## Trust & Transparency Benefits

### For modders

Modders no longer need to reverse-engineer what is and isn't moddable. A generated `MOD_SURFACES.md` gives them a complete, authoritative, machine-generated list. When a surface is `Disabled`, they know it's not a bug — it's a deliberate choice. When a surface is `Planned`, they know it's coming.

### For developers

Developers can enforce modding boundaries architecturally rather than through documentation conventions. The registry is the single source of truth. It compiles. It tests. It generates output. It cannot drift.

### For tooling

Because the capability matrix is generated from a typed, validated data structure, tooling can parse it reliably. CI pipelines can diff matrices between releases to detect surface regressions. NuGet packaging can embed the matrix as a resource. Certification submission can include it as evidence of controlled mod exposure.

---

## Console Safety Preservation

Phase 2 introduces no console-safety risks. Specifically:

| Risk Category | Phase 2 Impact |
|---------------|---------------|
| Scripting execution | None — no scripts introduced |
| Dynamic loading | None — no assembly loading |
| Reflection | None — no reflection used |
| Runtime toggles | None — status is static metadata |
| Config mutation | None — all declarations are immutable |
| Serialization changes | None — no serialization introduced |
| Extension pipeline changes | None — no pipeline integration |
| Audit log changes | None — audit logger unmodified |
| New dependencies | None — pure .NET BCL only |

The deterministic execution guarantee introduced in Phase 1 is fully preserved.

---

## What This Does NOT Introduce

To be unambiguous:

- **No scripting.** `ModSurfaceDeclaration` is data. It contains strings and enum values. It cannot execute anything.
- **No runtime execution changes.** `ModBridge.Validate()`, `AuditLogger`, and `BridgeConfig` are unmodified.
- **No reflection.** No `Type`, `Assembly`, `MethodInfo`, or `Activator` usage anywhere in Phase 2.
- **No dynamic loading.** No `Assembly.Load`, no plugin infrastructure, no late-bound types.
- **No new execution paths.** The validation firewall is unchanged.
- **No config mutation.** `ModSurfaceDeclaration` is sealed and immutable. `ModSurfaceRegistry` supports append-only registration.
- **Mods cannot register surfaces.** The registry is a host construct. Mod code has no access to it.

---

## Example Generated Matrix

```
=== DreamCraft: Legacies — Mod Capability Matrix ===

[BehaviorGraphs]
  EnemyAI [Limited]
    Enemy decision trees — read-only graph node configuration only.

[Data]
  CharacterStats [Enabled]
    Base and derived stat tables for all playable character classes.
  ItemDefinitions [Enabled]
    Item type definitions, rarity weights, and flavor text.
  WeaponBalance [Enabled]
    Weapon damage, range, and scaling factor tables.

[ProceduralInputs]
  DungeonLayout [Planned]
    Procedural dungeon generation weights. Target: Phase 4.
  WorldSeed [Disabled]
    Reserved for host control. Not exposed to mods.
```

---

## Forward Compatibility

Phase 2 is purely additive. The following guarantees hold for Phase 3 integration:

- No existing public APIs were modified.
- No execution interfaces were changed.
- No registries were modified (the existing `SchemaRegistry` pattern is untouched).
- No namespace structures used by Phase 3 were altered.
- All new types reside in `BridgeMod.Bridge` namespace alongside existing types.
- No constructors of existing types were modified.
- No existing test expectations were changed.

Phase 2 is safe to merge forward into Phase 3 without conflicts.

---

## Foundation

Phase 2 establishes a formal governance layer for mod exposure. Future phases will build upon this structured foundation while preserving deterministic execution guarantees.

---

*BridgeMod.SDK Phase 2 — Authored February 2026*
*DreamCraft: Legacies Project*
