# BridgeMod Architectural Decision Log

This document records the major architectural decisions in BridgeMod and the reasoning behind them. It's for developers, contributors, and anyone interested in understanding *why* we built things this way.

---

## Decision 1: 3-Gate Firewall Architecture (Phase 1)

**Date:** Early 2024
**Status:** Final (locked for all future phases)

### The Question
How do we validate mods in a way that:
1. Blocks injection attacks and malicious code
2. Prevents out-of-bounds stats from breaking the game
3. Provides operational visibility (audit trail)
4. Remains simple enough to explain and maintain

### The Decision
Implement three sequential validation gates:
1. **Type Check** — Schema validation, injection blocking
2. **Boundary Guards** — Stat clamping via configuration
3. **Audit Logging** — Append-only event trail

### Why This Approach

**Alternative 1: "Trust and verify"**
- Give mods direct access to game systems, catch errors in runtime
- ❌ Too late—damage already done
- ❌ No audit trail
- ❌ Unpredictable gameplay

**Alternative 2: Whitelist everything**
- Only allow explicitly approved values
- ❌ Too restrictive—modders can't create interesting content
- ❌ Game devs maintain huge whitelist
- ❌ Doesn't scale

**Alternative 3: Sandbox (our choice) + explicit gates**
- Validate early, fail fast, audit everything
- ✅ Consistent, predictable
- ✅ Scalable (dev configures one BridgeConfig)
- ✅ Transparent (every decision logged)
- ✅ Fast (<1ms per mod)

### Design Consequence
The 3-gate model is locked. All future phases (2, 3, 4, 5+) must respect these gates. This means:
- Behavior graphs must pass Gate 1 (schema validation)
- Procedural weights must pass Gate 2 (boundary guards)
- Everything must pass Gate 3 (audit logging)

This creates a unified security model across all mod types.

---

## Decision 2: Immutability Everywhere (Phase 1-4)

**Date:** Early 2024
**Status:** Final (locked)

### The Question
Should types be mutable? (e.g., `ModSurfaceDeclaration.Name = "new name"`)

### The Decision
All new types are sealed and immutable (no setters on properties, no public methods to modify state).

Examples:
- `ModSurfaceDeclaration` — sealed record, immutable
- `BehaviorGraphDefinition` — sealed, immutable
- `BridgeRandom` — sealed, state only modified internally
- `ProceduralWeightTable` — sealed, weights fixed at construction

### Why This Approach

**Alternative 1: Mutable objects**
- More flexibility for callers
- ❌ Introduces state bugs (who changed this? when?)
- ❌ Thread-safety becomes complex
- ❌ Determinism is fragile

**Alternative 2: Immutable-first (our choice)**
- Type is safer, easier to reason about
- ✅ Determinism guaranteed (state never changes)
- ✅ Thread-safe by default (no synchronization needed)
- ✅ Functional-style (easier testing)
- ✅ Copy-by-reference is safe

### Design Consequence
Callers can pass objects around without fear of side effects. Perfect for game engines that share mods across threads or replay systems.

---

## Decision 3: No System.Random (Phase 4)

**Date:** Feb 2026
**Status:** Final (locked)

### The Question
For procedural generation, should we use:
1. `System.Random` (framework library)
2. `System.Security.Cryptography.RNGCryptoServiceProvider` (cryptographic)
3. Custom Xorshift32 (pure bit operations)

### The Decision
Implement custom Xorshift32 PRNG from scratch.

```csharp
// BridgeRandom.cs
private uint _x = seed;

public uint Next()
{
    _x ^= _x << 13;
    _x ^= _x >> 17;
    _x ^= _x << 5;
    return _x;
}
```

### Why This Approach

**Alternative 1: System.Random**
- ✅ Built-in, no code to maintain
- ❌ Platform-dependent sequence (Windows ≠ Linux)
- ❌ Not designed for reproducibility
- ❌ Thread-unsafe (requires locking)

**Alternative 2: RNGCryptoServiceProvider**
- ✅ Cryptographically secure
- ❌ Overkill for loot tables (we don't need real randomness)
- ❌ Much slower (uses OS entropy)
- ❌ Non-reproducible by design

**Alternative 3: Xorshift32 (our choice)**
- ✅ **Deterministic** — same seed → identical sequence on all platforms
- ✅ **Fast** — 2-5ns per call (200M+ calls/sec)
- ✅ **Simple** — 3 lines of code, no dependencies
- ✅ **Portable** — works on every .NET platform (Windows, Mac, Linux, Console, Mobile)
- ✅ **Proven** — Xorshift is industry standard (Marsaglia, 2003)

### Design Consequence
All procedural generation is deterministic and reproducible. Same seed always produces the same world/loot/NPC names. Perfect for testing and console certification.

---

## Decision 4: Canonical Authority in SDK (Phase 3)

**Date:** Feb 2026
**Status:** Final (locked)

### The Question
Who defines mod validation rules?
1. The game engine (Unity, Godot, Unreal)
2. The SDK (BridgeMod)
3. Shared architecture (RFC/spec)

### The Decision
**The SDK is the canonical authority.** Engines implement adapters.

```
Game Engine (e.g., Unity)
    ↓
Adapter Layer (consumes pre-validated JSON from SDK)
    ↓
BridgeMod SDK (validates, sanitizes, audits)
    ↓
Raw Mod Data
```

### Why This Approach

**Alternative 1: Engine defines rules**
- ❌ Each engine has different validation
- ❌ Impossible for modders to know which game accepts what
- ❌ No portable certification

**Alternative 2: Shared spec (no enforcement)**
- ⚠️ Nice to have, but no teeth
- ⚠️ Engines can cheat (skip validation)
- ⚠️ No guarantee across platforms

**Alternative 3: SDK is authoritative (our choice)**
- ✅ Single source of truth (the C# library)
- ✅ Consistent across all engines and platforms
- ✅ Game engines focus on consuming data, not validating it
- ✅ Mods certified once, work everywhere
- ✅ Simpler for console certification (one library to audit)

### Design Consequence
BridgeMod SDK is the Rosetta Stone of modding. Mods validated by BridgeMod work on any engine that integrates the SDK adapter. This is critical for porting from PC to console without rearchitecting.

---

## Decision 5: Append-Only Audit Log (Phase 1)

**Date:** Early 2024
**Status:** Final (locked)

### The Question
How should we track validation decisions?
1. In-memory only
2. Append-only file/log
3. Database
4. Cloud service

### The Decision
Append-only, tamper-evident in-memory log with optional JSON export.

```csharp
public class AuditLogger
{
    private readonly List<AuditEntry> _entries = new();
    private readonly object _lock = new();

    public void Log(string modId, string code, string message)
    {
        lock (_lock)
        {
            _entries.Add(new AuditEntry { ... });
        }
    }

    public void FlushToDisk(string path)
    {
        lock (_lock)
        {
            var json = JsonConvert.SerializeObject(_entries);
            File.WriteAllText(path, json);
        }
    }
}
```

### Why This Approach

**Alternative 1: No logging**
- ❌ No visibility into mod decisions
- ❌ Impossible to debug issues
- ❌ Can't prove compliance for certification

**Alternative 2: Database**
- ✅ Scalable for thousands of games
- ❌ Adds SQL dependency
- ❌ Overkill for single-game mod loading
- ❌ Requires persistence layer

**Alternative 3: Cloud service**
- ✅ Centralized auditing
- ❌ Adds network dependency
- ❌ Privacy concerns (what gets logged?)
- ❌ Doesn't work offline

**Alternative 4: Append-only in-memory + optional export (our choice)**
- ✅ Simple, zero dependencies
- ✅ Works offline
- ✅ Thread-safe (lock-based)
- ✅ Optional persistence (you export if needed)
- ✅ Game devs decide where to store logs

### Design Consequence
Logging is optional but incentivized. Game devs can export the audit trail for console certification or post-mortem analysis. Tamper-evident (append-only) means no one can hide validation decisions.

---

## Decision 6: `ModManifest` as Canonical Mod Contract (Phase 3)

**Date:** Jan 2026
**Status:** Final (locked)

### The Question
What's the official "mod package format"? Should it be:
1. JSON (simple, human-readable)
2. ZIP with metadata (like Steam Workshop)
3. Custom binary format (fast, compact)
4. Engine-specific format (Unity AssetBundles, Godot scenes)

### The Decision
Simple JSON manifest + data files in a ZIP. The manifest shape is defined by `ModManifest` (sealed record) in the SDK.

```json
{
  "id": "unique_mod_id",
  "version": "1.0.0",
  "author": "modder_name",
  "gameId": "MyGame",
  "gameVersion": "1.0.0",
  "data": { /* mod data */ },
  "description": "Optional description",
  "tags": ["balance", "cosmetics"],
  "dependencies": ["other_mod_id"]
}
```

### Why This Approach

**Alternative 1: Custom binary**
- ✅ Compact, fast
- ❌ Not human-readable
- ❌ Requires custom tooling
- ❌ Incompatible with existing tools

**Alternative 2: Engine-specific**
- ✅ Optimized for one engine
- ❌ Not portable to other engines
- ❌ Console ports would require conversion

**Alternative 3: JSON + ZIP (our choice)**
- ✅ Human-readable (modders can write JSON)
- ✅ Standard tooling (any ZIP viewer, any JSON editor)
- ✅ Version control friendly (git handles JSON well)
- ✅ Platform-agnostic (works on PC, console, mobile)
- ✅ Extensible (add fields without breaking old mods)

### Design Consequence
Mods are portable and human-inspectable. A modder in Godot can understand a Unity mod by just reading the JSON. Perfect for multi-engine studios.

---

## Decision 7: Zero New Dependencies (Except Newtonsoft.Json) (Phase 1-4)

**Date:** Early 2024
**Status:** Strong (may be reconsidered for Phase 5+ cloud services)

### The Question
Can we depend on external NuGet packages, or should we implement everything ourselves?

### The Decision
Only one approved dependency: `Newtonsoft.Json 13.0.3` (for `AuditLogger.FlushToDisk` JSON export).

All other functionality (validation, graph execution, PRNG, etc.) is custom C#.

### Why This Approach

**Alternative 1: Minimal dependencies**
- ✅ Lightweight, fast to load
- ✅ Small attack surface
- ✅ Works on restricted platforms (some consoles)
- ✅ Easier to reason about code
- ❌ More code to maintain

**Alternative 2: Use libraries (NLog, Serilog, etc.)**
- ✅ Proven, battle-tested
- ❌ Adds complexity (more to understand)
- ❌ Transitive dependencies (dependency chains)
- ❌ Version conflicts in consumer projects

**Alternative 3: Zero dependencies (our choice, with one exception)**
- ✅ Minimal surface area
- ✅ No dependency version conflicts for users
- ✅ Works on every platform (no OS-specific libs)
- ✅ Console-friendly (no licensing issues with bundled libs)
- ✅ Fast load time
- Exception: Newtonsoft.Json for JSON export (mature, battle-tested, widely used)

### Design Consequence
BridgeMod is a lightweight, standalone library. Adding it to your project doesn't drag in 20+ transitive dependencies. Great for game engines and platforms with strict dependency policies.

---

## Decision 8: Behavioral Graphs Are Declarative, Not Executable (Phase 3)

**Date:** Jan 2026
**Status:** Final (locked)

### The Question
What should behavior graphs support?
1. Full scripting (Lua, C#, Python)
2. Declarative nodes only (no code)
3. Limited expression language (conditions only)

### The Decision
Declarative nodes only. Graphs define structure and transitions; game code provides behavior.

```csharp
// What mods CAN do (declarative):
var transition = new BehaviorTransition(
    from: "idle",
    to: "alert",
    trigger: "heard_sound",
    guard: new TransitionGuard("distance", GuardOperator.LessThan, 50.0f)
);

// What mods CANNOT do (code):
// ❌ var transition = new BehaviorTransition(
//     from: "idle",
//     to: "alert",
//     trigger: "heard_sound",
//     condition: () => Enemy.Position.Distance(Player.Position) < 50f
// );
```

### Why This Approach

**Alternative 1: Full scripting**
- ✅ Maximum flexibility for modders
- ❌ Security nightmare (mods run arbitrary code)
- ❌ Non-deterministic (side effects, randomness)
- ❌ Impossible to certify (what will the code do?)
- ❌ Breaks on engine updates (API changes)

**Alternative 2: Limited expression language**
- ⚠️ Partial solution
- ⚠️ Still complex to sandbox
- ⚠️ Still non-deterministic if expressions call functions

**Alternative 3: Declarative nodes (our choice)**
- ✅ Deterministic (no side effects)
- ✅ Safe (no code execution)
- ✅ Verifiable (static analysis possible)
- ✅ Portable (works across engines)
- ✅ Simple to test (no mocking needed)
- Trade-off: Less powerful, but safer

### Design Consequence
Behavior graphs are data structures, not programs. This makes them suitable for console certification and cross-platform compatibility. Modders express logic as structure, not code.

---

## Decision 9: Target .NET 8.0 + netstandard2.1 (Phase 1)

**Date:** Early 2024
**Status:** Final (locked)

### The Question
What .NET versions should BridgeMod support?

### The Decision
- **SDK targets:** `net8.0` + `netstandard2.1`
- **Tests target:** `net10.0`
- **CI tests:** .NET 10.0

### Why This Approach

**Alternative 1: .NET Framework only**
- ❌ Legacy, no longer supported by Microsoft
- ❌ Windows-only
- ❌ Doesn't work on console hardware

**Alternative 2: Latest .NET only (net10.0)**
- ✅ Modern, cutting-edge
- ❌ Breaks compatibility with older projects
- ❌ Requires all consumers to update

**Alternative 3: net8.0 + netstandard2.1 (our choice)**
- ✅ net8.0 = modern, LTS, widely adopted in games
- ✅ netstandard2.1 = compatibility with older projects, Unity support
- ✅ Broad coverage (PC, console, mobile)
- ✅ Forward-compatible (code written for net8.0 works on net10.0)

### Design Consequence
BridgeMod works in Unity, Godot (C#), and modern custom engines. Tests run on .NET 10.0 (latest) to ensure forward compatibility, but the library works with .NET 8.0+.

---

## Decision 10: Phases Are Sequential & Additive (Architecture)

**Date:** Early 2024
**Status:** Final (locked)

### The Question
How should we evolve BridgeMod?
1. Big bang (v1.0 with all features)
2. Sequential phases (Phase 1, 2, 3, ...)
3. Feature branches (users pick features)

### The Decision
Sequential, additive phases. Each phase adds capability without removing or breaking earlier phases.

- **Phase 1:** Firewall (Type Check → Boundary Guards → Audit Logging)
- **Phase 2:** Surfaces (Host declares what's moddable)
- **Phase 3:** Graphs (Deterministic state machines)
- **Phase 4:** Procedural (Deterministic PRNG + weight tables)
- **Phase 5+:** TBD (Cloud services, assets, browser)

Each phase is complete and tested before the next starts. No removing, only adding.

### Why This Approach

**Alternative 1: Big bang release**
- ✅ "Everything now"
- ❌ Takes years to develop
- ❌ High risk (big refactors)
- ❌ Users wait for all features
- ❌ Harder to get feedback

**Alternative 2: Feature branches**
- ✅ Flexibility
- ❌ Inconsistent API
- ❌ Users don't know what to use
- ❌ Maintenance nightmare

**Alternative 3: Sequential phases (our choice)**
- ✅ Fast iteration (get Phase 1 to market quickly)
- ✅ User feedback informs Phase 2
- ✅ Consistent architecture (each phase extends Phase N-1)
- ✅ Reduced risk (smaller changes)
- ✅ Additive = no breaking changes

### Design Consequence
BridgeMod evolves incrementally. Phase 1 ships today; Phase 2 learns from Phase 1 users; Phase 3 is informed by Phase 2 feedback. Users get value at each phase, not waiting for v1.0.

---

## What These Decisions Mean

### For Game Developers
- **Predictable:** You know what BridgeMod will do (it's deterministic)
- **Safe:** Mods can't break your game (3-gate validation)
- **Portable:** Same SDK on PC and console (canonical authority)
- **Extensible:** Each phase adds capability without breaking existing code

### For Modders
- **Transparent:** You understand the rules (declared surfaces)
- **Creative:** You have tools to make interesting mods (graphs, procedural)
- **Portable:** Your mod works across engines (JSON format, canonical contract)

### For Contributors
- **Predictable:** Clear scope for each phase
- **Maintainable:** No hidden state (immutable types)
- **Testable:** Pure functions, deterministic behavior
- **Explainable:** Decisions are documented (this file)

---

## Future Decisions Likely to Come

**Phase 5 (Cloud Services):**
- Will we depend on network libraries? (probably yes)
- Will we collect telemetry? (opt-in only)
- Will we use authentication? (optional)

**Phase 6+ (Assets):**
- Will we support asset mods? (likely yes)
- What validation gates for assets? (TBD)
- How do we handle licensing? (TBD)

These will be documented when they're made.

---

## How to Propose a Decision Change

If you think a decision is wrong:

1. **Open a GitHub Discussion** — Propose an alternative
2. **Explain the problem** — Why is the current decision inadequate?
3. **Present trade-offs** — What would we gain/lose?
4. **Get feedback** — Community weighs in
5. **Document** — If adopted, add to this file

All architectural decisions are reversible if the community agrees. But reversals must be unanimous (to avoid instability).

---

**Last Updated:** March 9, 2026
**Status:** v0.5.0 (Phases 1-4 complete)
**License:** MIT
