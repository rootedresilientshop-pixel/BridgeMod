# Experimental Notes

## DreamCraft Introspection — Schema Registry (Archived)

**Location:** `experimental/dreamcraft-introspection` branch

**Status:** Non-canonical. Reference implementation only.

**What It Was:**
The original Phase 2 work on `dev/phase-2-automation` explored an alternative mod surface declaration approach: auto-generating C# validation rules from Python engine dataclass definitions via a Python-to-C# code generator.

**Architecture:**
```
Python DreamCraft.Engine (@dataclass definitions)
    ↓
BridgeMod.Generator (introspection tool)
    ↓
src/BridgeMod.SDK/Generated/ (auto-generated .g.cs files)
    ↓
C# Firewall (compiled guards)
```

**Why It Is Non-Canonical:**
BridgeMod establishes **C# as the authoritative definition source**. Engines adapt to BridgeMod, not the reverse. Allowing the Python engine to define validation rules upstream would create a two-source-of-truth problem and couple the SDK to specific engine implementations.

**Why It Exists:**
The tooling demonstrates a useful pattern: **optional engine adapter infrastructure**. If the DreamCraft.Engine team wants to keep their dataclass definitions synchronized with C# guards, they can use this as a reference for building their own introspection and code generation workflow. This is valuable for multi-engine scenarios where each engine implements its own adapter layer.

**What It Contains:**
- `src/BridgeMod.Generator/` — Console tool for Python-to-C# code generation
- `src/BridgeMod.SDK/Generated/` — Example output (CharacterRules.g.cs, RetiredCharacterInputRules.g.cs)
- `src/BridgeMod.SDK/SchemaRegistry.cs` — Registry for managing generated rule sets
- `src/DreamCraft.Engine/simulation/scribe.py` — Simulation engine enhancements
- `src/DreamCraft.Engine/simulation/validation/` — Python-side validation logic
- `src/DreamCraft.Engine/tests/test_dynasty.py` — Python test fixtures
- `tests/Automation/` — Integration tests verifying the drift loop
- `tools/schema_sync.py` — Synchronization utility

**Test Coverage:**
The `DriftTests` class in `tests/Automation/DriftTests.cs` validates:
- New numeric fields produce guards in generated files
- Python default values become C# Max bounds
- Non-numeric fields are excluded
- Existing fields survive alongside new ones
- Optional numeric fields are still guarded
- Generated files carry auto-generated headers
- snake_case → PascalCase mapping works correctly

**Key Lessons:**
1. **Authority matters.** When multiple systems can define the same rule, drift is inevitable.
2. **Optional tooling is valuable.** This code isn't part of the core SDK, but it's a working example for engine teams.
3. **Adapter model scales.** Future engines can follow this pattern to stay in sync with their own source-of-truth definitions.

**Future Use:**
- Phase 3+ may reference this as an example of how to implement engine-specific adapters
- Teams integrating BridgeMod can examine this pattern for their own codebases
- Does NOT become part of the SDK. Remains experimental reference material.

**Why It Was Shelved:**
Phase 2 was redefined to focus on **Developer Mod Surfaces** (declarative governance layer) rather than **Schema Registry** (Python-to-C# code generation). The Surfaces approach is simpler, more additive, and avoids the two-source-of-truth problem entirely. Both approaches are valid for different use cases; BridgeMod chose the approach that keeps the SDK as the single authoritative source.

---

*Archived February 20, 2026*
*Branch: experimental/dreamcraft-introspection*
*Stash preserved for reference and future architectural exploration*
