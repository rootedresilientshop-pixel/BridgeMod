# Engine Adapter Model

**BridgeMod Architecture: Multi-Engine Support via Canonical Contract**

---

## Core Principle

**BridgeMod defines the canonical mod contract. Engines implement adapters.**

```
┌─────────────────────────────────────────────────────────┐
│ Game Developer                                          │
│ (declares mod surfaces, integrates BridgeMod SDK)       │
└─────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────┐
│ BridgeMod SDK (C# — Canonical Authority)               │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ 1. Validate manifest                               │ │
│ │ 2. Deserialize payload                             │ │
│ │ 3. Apply boundary guards (configurable)            │ │
│ │ 4. Audit log decisions                             │ │
│ └─────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
                           ↓
         ┌─────────────────────────────────────┐
         │  Sanitized, Valid JSON              │
         │  (guaranteed by firewall)            │
         └─────────────────────────────────────┘
                           ↓
        ┌──────────────────────────────────────┐
        │ Engine Adapter Layer (per engine)    │
        │ • DreamCraft.Engine                 │
        │ • Unity (custom integration)        │
        │ • Godot (custom integration)        │
        │ • Other C# runtimes                 │
        └──────────────────────────────────────┘
                           ↓
        ┌──────────────────────────────────────┐
        │ Game Runtime (consumes clean data)   │
        └──────────────────────────────────────┘
```

---

## What the SDK Guarantees

The C# SDK is the **single authoritative source** for:

| Aspect | Owner | Scope |
|--------|-------|-------|
| Mod manifest shape | BridgeMod SDK | Zip + manifest.json + data files |
| Payload schema | BridgeMod SDK | JSON structure and field names |
| Validation rules | BridgeMod SDK | Boundary guards, type checks, format validation |
| Audit logging | BridgeMod SDK | Record of all validation decisions |
| Deterministic behavior | BridgeMod SDK | No stochasticity in validation or bounds checks |

**The engine does not define these.** The engine consumes the output of the BridgeMod firewall.

---

## What Engines Must Implement

Each engine implements an **adapter** that:

| Task | Why | Example |
|------|-----|---------|
| Accept validated JSON | BridgeMod guarantees it's safe | DreamCraft.Engine receives scrubbed character definitions |
| Map to internal types | Convert JSON to engine-native structures | `{ "health": 100 }` → `Character.health = 100` |
| Provide safe defaults | Handle missing fields gracefully | If a field is missing, use a sensible default |
| Report errors to host | Fail safely if something unexpected occurs | Log to host callback; don't crash |

**The adapter does not re-validate data.** That would defeat the purpose of the firewall.

---

## Why This Model Exists

### Problem: Validation Drift

If multiple systems define validation rules:
- Rule A in Python, Rule B in C#, Rule C in C# → Rule mismatch
- Developer changes Rule A; Rules B and C become stale
- Mods pass validation but crash at runtime

### Solution: Single Source of Truth

- **One system defines validation:** the C# SDK
- **All other systems consume output:** engines, tools, documentation
- **No other system can override:** rules are immutable at runtime

---

## Multi-Engine Example

### Scenario
BridgeMod v0.3+ will support multiple game engines:
- DreamCraft.Engine (Python)
- Custom Unity integrations
- Custom Godot integrations
- Proprietary C# runtimes

All use the **same SDK**. All get **the same validation**. Each implements **its own adapter**.

### Concrete: Character Health Field

**BridgeMod SDK defines:**
```csharp
// In BridgeConfig
MaxStatValue = 9999  // Upper bound for numeric fields
```

**All engines respect this:**
```
Input: { "health": 999999 }
Validation: 999999 > 9999 → clamp to 9999
Output: { "health": 9999 }
```

**Adapters consume the output:**
- DreamCraft.Engine: `character.health = 9999`
- Unity: `player_character.SetHealth(9999)`
- Custom C#: `entity.ApplyStat("health", 9999)`

**No engine re-interprets the value.** No drift.

---

## Experimental Tooling

The `experimental/dreamcraft-introspection/` branch contains an example of how the **DreamCraft.Engine** team could build **optional automation** to keep their Python dataclass definitions in sync with SDK validation rules.

This is **reference material**, not authoritative:
- Demonstrates the **adapter pattern**
- Shows how an engine can **introspect its own definitions**
- Produces **suggestions** (not binding rules) for the SDK team
- Remains **optional and non-canonical**

Engine teams can examine this approach when building their own integration tooling.

---

## Designing a New Adapter

If you're integrating BridgeMod with a new engine:

### 1. Accept Validated JSON
```csharp
string sanitizedJson = bridgemod.Validate(rawModData);
// sanitizedJson is now guaranteed safe
```

### 2. Parse to Internal Types
```csharp
var modData = JsonConvert.DeserializeObject<MyEngineModDefinition>(sanitizedJson);
```

### 3. Apply Safe Defaults
```csharp
modData.CharacterName ??= "Unknown";
modData.Health ??= 100;  // Default from your engine, not from mod
```

### 4. Report Errors Safely
```csharp
try
{
    ApplyModData(modData);
}
catch (InvalidOperationException ex)
{
    Log($"Mod adaptation failed (safe): {ex.Message}");
    // Continue with unmodified game state — mod is disabled
}
```

### 5. Never Re-Validate
```csharp
// DON'T do this:
if (modData.Health > 9999) modData.Health = 9999;  // Wrong — firewall already did this

// DO this:
ApplyCharacterStat(modData.Health);  // Trust the firewall
```

---

## Forward Compatibility

### What Will Never Change (Locked)
- Canonical contract defined by BridgeMod SDK
- Validation rule semantics
- Manifest shape
- Audit logging format

### What Can Evolve (Additive Only)
- New optional fields in payloads
- New optional validation rules (applied uniformly to all engines)
- New optional surface categories
- New optional engine adapter patterns

### Version Lock
Adapters target a **specific BridgeMod version**. When you update BridgeMod, re-test your adapter. The contract is stable, but new features may be available.

---

*Last updated: February 2026*
*BridgeMod v0.3.0 Architecture*
