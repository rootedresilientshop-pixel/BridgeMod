# BridgeMod Security Architecture: The 3-Gate Firewall

**Quick Summary:** Every mod passes through three sequential validation gates before your game ever sees it. Type Check → Boundary Guards → Audit Logging. This is how BridgeMod stays deterministic, predictable, and safe.

---

## The 3-Gate Model

```
┌─────────────────────────────────────────────────────────────┐
│  RAW MOD DATA (untrusted JSON + manifest)                   │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
    ┌────────────────────────────────┐
    │  GATE 1: TYPE CHECK            │
    │  ✓ JSON deserializable?        │
    │  ✓ Manifest shape correct?     │
    │  ✓ No injection payloads?      │
    │                                │
    │  REJECTION: ParseErr001        │
    │  (entire payload blocked)      │
    └────────────┬───────────────────┘
                 │
                 ▼
    ┌────────────────────────────────┐
    │  GATE 2: BOUNDARY GUARDS       │
    │  ✓ Stats within limits?        │
    │  ✓ Clamping applied (if needed)│
    │  ✓ Config boundaries enforced? │
    │                                │
    │  CORRECTION: BoundClamp003     │
    │  (value adjusted, payload OK)  │
    └────────────┬───────────────────┘
                 │
                 ▼
    ┌────────────────────────────────┐
    │  GATE 3: AUDIT LOGGING         │
    │  ✓ All decisions recorded      │
    │  ✓ Thread-safe append-only log │
    │  ✓ Exportable to JSON          │
    │                                │
    │  LOGGED: Every validation step │
    │  (enables post-mortem analysis)│
    └────────────┬───────────────────┘
                 │
                 ▼
    ┌────────────────────────────────┐
    │  ✅ SANITIZED MOD PAYLOAD      │
    │  (safe to load into game)      │
    └────────────────────────────────┘
```

---

## Gate 1: Type Check (Schema Validation)

**What it does:** Validates that raw mod data conforms to the expected shape.

**Code location:** [BridgeMod.SDK/BridgeMod.Bridge.cs](../src/BridgeMod.SDK/BridgeMod.Bridge.cs)

**Enforced rules:**
- JSON must deserialize to valid `ModManifest` (Phase 3 canonical contract)
- Required fields: `id`, `version`, `author`, `gameId`, `gameVersion`
- Optional fields: `description`, `tags`, `dependencies`
- No executable content (no scripts, no code, no reflection)
- String fields checked for injection markers: `<script>`, `javascript:`, `eval`, etc.

**Rejection code:** `ParseErr001` (PARSE_ERR_001)

**Example:**
```csharp
// ✅ Valid mod
{
  "id": "better_weapons",
  "version": "1.2.0",
  "author": "modder123",
  "gameId": "MyGame",
  "gameVersion": "1.0.0",
  "data": { "weapons": { ... } }
}

// ❌ Rejected — injection attempt
{
  "id": "malicious",
  "description": "<script>alert('xss')</script>",
  ...
}
// Result: ParseErr001, entire payload blocked
```

---

## Gate 2: Boundary Guards (Value Clamping)

**What it does:** Ensures numeric values stay within developer-configured limits.

**Code location:** [BridgeMod.SDK/BridgeMod.Bridge.cs](../src/BridgeMod.SDK/BridgeMod.Bridge.cs) — `BridgeConfig` class

**Configuration (BridgeConfig):**
```csharp
var config = new BridgeConfig
{
    MaxStatValue = 9999,    // Cap all stats at this ceiling
    MinStatValue = 0        // Floor at zero (no negatives)
};

var bridge = new ModBridge(config);
```

**Enforced rules:**
- Any stat > `MaxStatValue` gets clamped down
- Any stat < `MinStatValue` gets clamped up
- Clamping is **non-fatal** (payload still loaded, but with corrected values)
- Clamp events logged for audit trail

**Correction code:** `BoundClamp003` (BOUND_CLAMP_003)

**Example:**
```csharp
// Mod declares: health = 999999
// Config max: 9999
// Result: health clamped to 9999 + BoundClamp003 logged

// Outcome: Mod loads successfully, but with safe values
```

---

## Gate 3: Audit Logging (Operational Tracing)

**What it does:** Records every validation decision in an append-only, tamper-evident log.

**Code location:** [BridgeMod.SDK/BridgeMod.Bridge.cs](../src/BridgeMod.SDK/BridgeMod.Bridge.cs) — `AuditLogger` class

**Features:**
- **Thread-safe:** Protected by internal lock; safe for concurrent mod loading
- **Append-only:** No deletions, modifications, or overwrites (tamper-evident)
- **Exportable:** `AuditLogger.FlushToDisk(path)` → JSON file with complete history
- **Error codes:** Every log entry has a code (ParseErr001, BoundClamp003, etc.)

**Log entry structure:**
```json
{
  "timestamp": "2026-03-09T14:23:45.1234567Z",
  "modId": "better_weapons",
  "errorCode": "BOUND_CLAMP_003",
  "message": "Health stat clamped from 999999 to 9999"
}
```

**Usage:**
```csharp
var logger = new AuditLogger();
var bridge = new ModBridge(config, logger);

// Load and validate mods...
// bridge.Validate(rawModData);

// Export audit trail for analysis
logger.FlushToDisk("/var/log/bridgemod_audit.json");
// File now contains full validation history
```

**Phases using audit logging:**
- **Phase 3:** BehaviorGraphExecutor logs `WarnStuck001` when no transitions match (stuck state detection)
- **Phase 4:** BridgeRandom logs `ProcGen001` on seed initialization (reproducibility tracking)

---

## Why 3 Gates?

### Defense in Depth

Each gate serves a distinct purpose:

| Gate | Purpose | Failure Mode | Impact |
|------|---------|--------------|--------|
| **Type Check** | Schema integrity | Rejects entire mod | Security |
| **Boundary Guards** | Runtime safety | Clamps values | Stability |
| **Audit Log** | Operational visibility | Records everything | Compliance |

### Determinism Guarantee

Each gate produces **identical output** for identical input:
- Same mod JSON → Same validation result
- Same boundary config → Same clamping
- Same timestamp (optional) → Same log entry

This makes BridgeMod suitable for:
- ✅ Replaying mod loads (testing)
- ✅ Console certification (consistent behavior)
- ✅ Multiplayer sync (all clients agree on validation)

---

## Integration Example: Unity Game

```csharp
public class ModManager : MonoBehaviour
{
    private AuditLogger _auditLogger;
    private ModBridge _bridge;

    void Start()
    {
        // Configure boundaries
        var config = new BridgeConfig
        {
            MaxStatValue = 9999,
            MinStatValue = 0
        };

        // Create audit trail
        _auditLogger = new AuditLogger();

        // Initialize firewall
        _bridge = new ModBridge(config, _auditLogger);

        // Load mod files
        LoadModsFromDirectory("Assets/Mods/");

        // Export audit trail for post-game analysis
        string auditPath = $"{Application.persistentDataPath}/audit_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        _auditLogger.FlushToDisk(auditPath);
    }

    private void LoadModsFromDirectory(string directory)
    {
        foreach (var modFile in Directory.GetFiles(directory, "*.json"))
        {
            try
            {
                string rawJson = File.ReadAllText(modFile);
                var result = _bridge.Validate(rawJson);

                if (result.IsValid)
                {
                    // Gate 1 & 2 passed, load mod
                    ApplyModData(result.Payload);
                }
                else
                {
                    // Rejected at Gate 1
                    Debug.LogWarning($"Mod rejected: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading mod {modFile}: {ex.Message}");
            }
        }
    }

    private void ApplyModData(ModPayload payload)
    {
        // Payload is guaranteed safe by all 3 gates
        // Apply data to game systems...
    }
}
```

---

## Security Properties

### What BridgeMod Protects Against

| Threat | Gate | Mitigation |
|--------|------|-----------|
| Code injection (`<script>` tags) | Gate 1 | Schema validation rejects markup |
| Out-of-bounds stats (999999 health) | Gate 2 | Boundary guards clamp values |
| Denial of service (infinite loops) | Gate 1 | No executable content allowed |
| Tampering (log modification) | Gate 3 | Append-only audit trail |
| Untracked changes | Gate 3 | Every decision logged with timestamp |

### What BridgeMod Does NOT Protect Against

- ❌ Asset file exploits (use your engine's asset pipeline validation)
- ❌ Networked cheating (validate mods on server, not client alone)
- ❌ Compromised game binary (your executable is the trust anchor)
- ❌ Local file system attacks (BridgeMod assumes OS-level security)

**Principle:** BridgeMod is a **mod-specific firewall**, not a full security solution. Use it alongside your game engine's existing security model.

---

## Error Codes Reference

See [BridgeMod.SDK/BridgeMod.Bridge.cs](../src/BridgeMod.SDK/BridgeMod.Bridge.cs) for all error codes.

**Common codes you'll see:**
- `PARSE_ERR_001` — Injection attempt or schema mismatch (Gate 1 rejection)
- `BOUND_CLAMP_003` — Stat value clamped by boundary guard (Gate 2 correction)
- `WARN_STUCK_001` — Behavior graph event matched no transitions (Phase 3 informational)
- `PROCEDURAL_GEN_001` — Random seed initialized for reproducibility (Phase 4 informational)

---

## Testing & Determinism Proof

BridgeMod's 3-gate model is tested with:
- **11 Phase 1 tests:** Gate validation, injection blocking, boundary clamping
- **Determinism proof:** 1000-iteration tests verify identical output from identical input
- **Thread-safety tests:** Concurrent audit logging verified

All tests pass with **zero warnings** in Release configuration.

See [tests/Phase1Tests.cs](../tests/Phase1Tests.cs) for implementation details.

---

## Next Steps

1. **For game developers:** See [QUICKSTART.md](../QUICKSTART.md) to integrate BridgeMod into your game
2. **For modders:** See [MOD_SCHEMA.md](../MOD_SCHEMA.md) to learn what you can mod
3. **For security researchers:** See [Phase1_Firewall_Design.md](Phase1_Firewall_Design.md) for deep-dive (if available)
4. **Questions?** Open a discussion in [GitHub Discussions](https://github.com/rootedresilientshop-pixel/BridgeMod/discussions)

---

**BridgeMod Security Architecture © 2026 DreamCraft: Legacies**
**License: MIT** — [See LICENSE](../LICENSE)
