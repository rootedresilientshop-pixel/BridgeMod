# BridgeMod.sdk

[![NuGet Version](https://img.shields.io/nuget/v/BridgeMod.sdk?label=NuGet&color=blue&logo=nuget)](https://www.nuget.org/packages/BridgeMod.sdk)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Build Status](https://img.shields.io/github/actions/workflow/status/dreamcraft/bridgemod-sdk/ci.yml?branch=main)](https://github.com/dreamcraft/bridgemod-sdk/actions)
[![SDK Version](https://img.shields.io/badge/SDK-0.2.2-brightgreen)](CHANGELOG.md)

> A security-first SDK for bridging PC modding toolchains with console game engines.

---

## The Problem: The PC-Console Modding Gap

PC game modding has a rich, open ecosystem. Tools like Nexus Mod Manager, custom asset pipelines,
and community-built editors allow players to reshape their game worlds freely. **Consoles do not.**

Console platforms operate in a locked, trusted execution environment. Every asset, script, and
data packet that touches a console's game state must be **verified and sanitized** before it is
allowed in. There are two core reasons:

| Risk | Description |
|------|-------------|
| **Untrusted Data** | A mod file authored on a PC has no chain of custody. It may contain malformed data, buffer overflows, or values that exploit unchecked boundaries in the game engine. |
| **Security Boundary Violation** | Consoles enforce strict memory and process sandboxes. A single unvalidated integer — passed as an array index — can overwrite protected memory, crash the title, or worse. |

The gap is not a technology problem. It is a **trust problem.** PC mods are born in an untrusted
environment and must earn their way into a trusted one.

---

## The Solution: A Modding Firewall

BridgeMod.sdk acts as a **firewall** between the wild west of PC mod files and the locked gate of
the console game runtime.

Just as a network firewall inspects every packet against a ruleset before forwarding it, BridgeMod
inspects every mod payload against a validation schema before it is allowed to influence game state.

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        PC MOD TOOLCHAIN                         │
│  (Nexus, Custom Editor, JSON/XML Payloads, Community Scripts)   │
└──────────────────────────────┬──────────────────────────────────┘
                               │  Raw / Untrusted Payload
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                      BridgeMod.sdk  v0.2.2                      │
│                                                                 │
│  ┌─────────────┐   ┌──────────────┐   ┌──────────────────────┐ │
│  │  Deserialize│──▶│  Boundary    │──▶│  Audit Logger        │ │
│  │  & Parse    │   │  Guards      │   │  (tamper-evident log)│ │
│  └─────────────┘   └──────┬───────┘   └──────────────────────┘ │
│                           │                                     │
│                    PASS / REJECT                                 │
└─────────────────────────────────────────────────────────────────┘
                               │  Sanitized / Verified Payload
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                   CONSOLE GAME RUNTIME                          │
│  (DreamCraft: Legacies Simulation Engine — Trusted Boundary)    │
└─────────────────────────────────────────────────────────────────┘
```

Every payload traverses three gates:

1. **Deserialize & Parse** — Structural integrity check. Malformed data is rejected before it
   touches any game logic.
2. **Boundary Guards** — Value-range enforcement. Stat values, item counts, and entity IDs are
   clamped or rejected against defined schemas.
3. **Audit Logger** — Every accepted and rejected payload is recorded with a timestamp and reason
   code, producing a tamper-evident trail for post-incident analysis.

---

## Verification Summary

The following test cases validate the three critical security layers of BridgeMod.sdk 0.2.2:

| # | Test Case | Payload Type | Expected Result | Status |
|---|-----------|-------------|-----------------|--------|
| 1 | **Standard Mod Load** | Valid character JSON, all values in-bounds | Payload accepted, character state updated | PASS |
| 2 | **Malicious Payload Rejection** | Injected script tag inside a string field | Deserializer strips tag; payload rejected with `PARSE_ERR_001` | PASS |
| 3 | **Boundary Guard — Stat Overflow** | `health: 999999` (max allowed: `9999`) | Value clamped to `9999`; audit log entry written with `BOUND_CLAMP_003` | PASS |

Full test source: [tests/test_pulse_engine.py](tests/test_pulse_engine.py) | [Legacies_Bridge_Test/pulse_test.py](Legacies_Bridge_Test/pulse_test.py)

---

## Quick Start

### Requirements

- .NET 6.0+ (for C# bridge layer)
- Python 3.10+ (for simulation engine)
- Docker (optional, recommended for full stack)

### Installation

```bash
# Install the SDK via NuGet
dotnet add package BridgeMod.sdk --version 0.2.2

# Clone the repository and run the sample
git clone https://github.com/dreamcraft/bridgemod-sdk.git
cd bridgemod-sdk
docker compose up --build -d
```

### Run the Bridge Test Sample

```bash
# Python simulation engine (the trusted side)
python Legacies_Bridge_Test/pulse_test.py

# C# bridge layer (the mod intake side)
dotnet run --project Legacies_Bridge_Test
```

See [Legacies_Bridge_Test/Sample_Walkthrough.md](Legacies_Bridge_Test/Sample_Walkthrough.md) for a
step-by-step developer tutorial.

---

## Project Structure

```
dreamcraft_v2/
├── api/                        # FastAPI REST + WebSocket layer
├── data/                       # Schema, seed data, DB setup
├── llm/                        # Ollama-compatible LLM client
├── simulation/                 # Core 4-stage pulse engine
├── Legacies_Bridge_Test/       # Sample: C# Bridge + Python Engine
│   ├── Program.cs              # C# SDK entry point (annotated)
│   ├── pulse_test.py           # Python bridge validation tests
│   └── Sample_Walkthrough.md  # Developer tutorial
├── scripts/                    # Deployment and sync utilities
├── tests/                      # Pytest test suite
├── CHANGELOG.md                # Version history
└── README.md                   # This file
```

---

## Contributing & Using in Unity

BridgeMod.sdk is designed to be embedded in any C# game engine environment — including Unity.

**For full integration guidance, roadmap, and console platform targets, see:**
[console_modding_execution_plan.md](console_modding_execution_plan.md)

### How to Contribute

1. Fork this repository and create a feature branch from `main`.
2. All new validation rules must include a corresponding test in `tests/` and an entry in
   `CHANGELOG.md`.
3. For security-sensitive changes (boundary guard logic, audit logger), request review from a
   maintainer before merging.
4. Open a pull request with a clear description of the problem solved and the verification
   approach used.

### Unity Integration (Quick Reference)

```csharp
// 1. Add BridgeMod.sdk to your Unity project via NuGet for Unity or manual DLL reference.
// 2. Initialize the bridge in your GameManager Awake() method.

using BridgeMod.Bridge;

var bridge = new ModBridge(new BridgeConfig
{
    MaxStatValue   = 9999,
    EnableAuditLog = true,
    AuditLogPath   = Application.persistentDataPath + "/bridgemod_audit.log"
});

// 3. Pass any incoming mod payload through the bridge before applying it to game state.
var result = bridge.Validate(incomingModPayload);
if (result.IsValid)
{
    ApplyToGameState(result.SanitizedPayload);
}
```

---

## License

MIT License — see [LICENSE](LICENSE) for details.

> Built for DreamCraft: Legacies — a Cloud-Sovereign medieval simulation platform.
> Oracle Cloud ARM · Raspberry Pi 5 · Offline-first · Player-owned data.
