# Console Modding Execution Plan

> **Status:** Active — v0.2.2 complete. Next milestone: v0.3.0 (Schema Registry).
> **Last updated:** 2024-02-16

---

## Vision

Enable console players to apply community-created mods from PC toolchains
to DreamCraft: Legacies — safely, without violating the console platform's
security model or the player's data sovereignty.

The guiding principle: **the player's console is a trusted vault.** Nothing
enters it that has not been inspected and cleared by BridgeMod.sdk.

---

## Phase Roadmap

### Phase 1 — Foundation (COMPLETE: v0.2.2)

**Goal:** Prove the security bridge concept with a working prototype.

| Deliverable | Status |
|-------------|--------|
| C# `ModBridge` with 3-gate pipeline | DONE |
| Python `BridgeValidator` matching C# logic | DONE |
| `AuditLogger` with `PARSE_ERR_001` and `BOUND_CLAMP_003` | DONE |
| Boundary Guards for 5 core stat fields | DONE |
| 3-case verification test suite | DONE |
| Sample folder with C# + Python reference code | DONE |
| Professional README with architecture diagram | DONE |

---

### Phase 2 — Schema Registry (Target: v0.3.0)

**Goal:** Move hardcoded validation rules out of the bridge code and into
a versioned, pluggable schema registry.

| Task | Description | Priority |
|------|-------------|----------|
| `ModSchema` type | A declarative schema that defines allowed fields, types, and ranges for a given mod type (character, item, faction). | HIGH |
| `SchemaRegistry` class | Load schemas from JSON files at runtime. Validate a payload against the schema for its `mod_type` field. | HIGH |
| Schema versioning | Schemas carry a `schema_version` field. The registry supports side-by-side versions so old mods still validate against the schema they were built for. | MEDIUM |
| Schema distribution | Publish official schemas to a CDN endpoint. The bridge can pull schema updates without a full SDK update. | LOW |

**Why this matters:** Hardcoded rules can't keep up with game updates. A
living schema registry lets the DreamCraft content team add new item types
and stat fields without shipping a new SDK version.

---

### Phase 3 — Console Platform Integration (Target: v0.4.0)

**Goal:** Validate the bridge on a real console certification test environment.

| Task | Description |
|------|-------------|
| PlayStation certification profile | Map Sony's title security requirements to BridgeMod schema constraints. |
| Xbox certification profile | Map Microsoft's title update rules to the bridge's rejection policy. |
| Nintendo profile | Validate that the bridge satisfies Nintendo's strict no-untrusted-code policy. |
| Platform-specific audit log format | Each platform's compliance team may require a specific log format. Implement adapters. |

**Constraint:** Console platform SDKs are under NDA. This phase requires
developer program membership for each target platform. Currently targeting
PlayStation first (active dev program membership).

---

### Phase 4 — Unity Package (Target: v0.5.0)

**Goal:** Ship BridgeMod.sdk as a first-class Unity Package Manager (UPM) package.

#### Unity Integration Architecture

```
Unity Editor (PC)
├── BridgeMod.Unity package (UPM)
│   ├── ModBridge.cs          ← same core logic as Program.cs sample
│   ├── BridgeConfig.asset    ← ScriptableObject — configure without code
│   └── BridgeModWindow.cs    ← Editor window for testing payloads
└── Build output
    └── Builds/Console/       ← bridge runs on-device, validates at load time
```

#### UPM Installation

```bash
# Via Unity Package Manager → Add package from git URL:
https://github.com/dreamcraft/bridgemod-sdk.git#upm

# Or via manifest.json:
{
  "dependencies": {
    "com.dreamcraft.bridgemod-sdk": "0.5.0"
  }
}
```

#### Minimal Unity Usage

```csharp
using BridgeMod.Bridge;
using UnityEngine;

public class ModLoader : MonoBehaviour
{
    [SerializeField] private BridgeConfigAsset bridgeConfig;

    private ModBridge _bridge;

    private void Awake()
    {
        _bridge = new ModBridge(bridgeConfig.ToConfig());
    }

    /// <summary>
    /// Call this from your mod loading pipeline before applying any mod data
    /// to character stats, inventory, or world state.
    /// </summary>
    public bool TryApplyMod(string rawModJson)
    {
        var payload = JsonUtility.FromJson<Dictionary<string, object>>(rawModJson);
        var result  = _bridge.Validate(payload, payloadId: Guid.NewGuid().ToString());

        if (!result.IsValid)
        {
            Debug.LogWarning($"[BridgeMod] Mod rejected: {result.ErrorCode}");
            return false;
        }

        ApplyToGameState(result.SanitizedPayload);
        return true;
    }

    private void ApplyToGameState(Dictionary<string, object> sanitized)
    {
        // Your game-specific logic here.
        // At this point, all string fields are markup-free and all
        // stat values are within their declared boundaries.
    }
}
```

---

### Phase 5 — Mod Marketplace Integration (Target: v1.0.0)

**Goal:** Allow console players to browse and install community mods directly
from a curated marketplace, with BridgeMod validation as the gate.

| Feature | Description |
|---------|-------------|
| Mod signing | Mod authors sign their packages with a private key. The bridge verifies the signature before running schema validation. |
| Reputation scoring | Mods that generate zero audit log entries across 1000+ installs receive a "Verified" badge. |
| Automatic schema generation | Analyze a mod package and suggest the minimal schema that validates it, reducing author friction. |
| Rollback support | If a mod causes a post-validation issue in the engine, the audit log entry allows the exact payload to be replayed in a test environment for diagnosis. |

---

## Security Commitments

These properties must hold at every release:

1. **No unvalidated payload ever reaches the simulation engine.** The engine's
   API must reject any request that does not carry a valid bridge token.

2. **The audit log is append-only.** No code path in the bridge may modify or
   delete an existing audit entry.

3. **Rejection is the safe default.** When in doubt — malformed JSON, unknown
   field types, schema version mismatch — the bridge rejects and logs. It never
   silently accepts ambiguous input.

4. **Schema changes are backward-compatible.** Adding a new field to a schema
   is allowed. Removing or renaming a field requires a new major schema version.

---

## Contributing to This Plan

This document is a living roadmap. To propose a change:

1. Open an issue on the repository with the label `roadmap`.
2. Reference the phase and task you are proposing to add, modify, or remove.
3. Include a one-paragraph rationale and any relevant prior art (other SDKs,
   platform documentation, security research).

---

*Back to [README.md](README.md) · See also [CHANGELOG.md](CHANGELOG.md)*
