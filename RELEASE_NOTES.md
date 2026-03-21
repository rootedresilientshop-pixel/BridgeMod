# BridgeMod v0.6.0 Release Notes

**Release Date:** March 2026
**Status:** Interface Alpha — Ready for Project Thalamus Integration
**Test Coverage:** 131/131 passing ✅
**Build:** 0 warnings, 0 errors ✅

---

## What's New in v0.6.0

### 🔐 Project Manifest & Governance Tracking

**New:** The `ProjectManifest` class gives you a "security dashboard" snapshot of your mod system:

```csharp
var manifest = bridge.GenerateManifest("MyGame", "1.0.0", registry, tables, graphs);
var json = manifest.ToJson(); // For Thalamus visual IDE to consume
```

Includes:
- **ProjectId** & **Version** — Your project metadata
- **Timestamp** — When the manifest was generated (UTC)
- **ActiveSurfacesCount** — How many mod surfaces you've declared
- **LogicFingerprint** — SHA256 hash of all surfaces, weight tables, and behavior graphs
- **GovernanceStatus** — `Ungoverned`, `PendingAudit`, or `GovernedByKanon`

### 🎯 Logic Fingerprinting Engine

**New:** `GenerateLogicFingerprint()` creates a deterministic SHA256 hash of your entire mod governance surface:

```csharp
string fingerprint = bridge.GenerateLogicFingerprint(
    registry: surfaceRegistry,
    tables: weightTables,
    graphs: behaviorGraphDefinitions
);
// Returns: "a1b2c3d4e5f6..." (64-char lowercase hex)
```

**Why?** This fingerprint is what Kanon (or other governance systems) will cryptographically sign for console certification. Same input → Same output, always. Platform-independent (netstandard2.1 ✅ net8.0 ✅).

### 📊 Governance Status in Audit Logs

**Enhanced:** Every audit log entry now reports your governance status:

```
[UNGOVERNED] [2025-03-21T14:30:45.1234567Z] [PARSE_ERR_001] payload=mod_xyz :: Disallowed markup in field 'description'
[GOVERNED_BY_KANON] [2025-03-21T14:31:02.5678901Z] [BOUND_CLAMP_003] payload=mod_abc :: Field 'health' clamped: 50000 -> 9999
```

Set the certificate to flip the status:

```csharp
bridge.GovernanceCertificate = "KANON-CERT-ABC123...";
// Now all new logs are prefixed [GOVERNED_BY_KANON]
```

### 🏗️ New Architecture

**Namespace:** `BridgeMod.Bridge.Models`
- `GovernanceStatus` enum: `Ungoverned`, `PendingAudit`, `GovernedByKanon`
- `ProjectManifest` class with `ToJson()` for clean JSON export

**New Methods on `ModBridge`:**
- `GenerateLogicFingerprint(...)` — Deterministic fingerprinting
- `GenerateManifest(...)` — Manifest factory
- `GovernanceCertificate` property — Set certificate, status flips automatically
- `CurrentGovernanceStatus` property — Computed from certificate presence

**Enhanced:** `AuditLogger` now tracks `GovernanceStatus` and prefixes all entries.

---

## Backward Compatibility

✅ **100% backward compatible.** All 100+ pre-existing tests pass unchanged.

- `AuditLogger.HasCode()` still works (governance prefix doesn't break substring search)
- `ModBridge.Validate()` behavior unchanged
- No breaking changes to any public API
- All existing code runs as-is

---

## Integration with Project Thalamus & Kanon

### Standalone (Default)

```csharp
var bridge = new ModBridge(config);
var manifest = bridge.GenerateManifest("MyGame", "1.0.0");
// manifest.GovernanceStatus == Ungoverned
// All logs: [UNGOVERNED] ...
```

### With Kanon (Optional)

```csharp
var bridge = new ModBridge(config);
bridge.GovernanceCertificate = kanon.RequestCertificate(...);;
var manifest = bridge.GenerateManifest("MyGame", "1.0.0");
// manifest.GovernanceStatus == GovernedByKanon
// All logs: [GOVERNED_BY_KANON] ...
```

### For Thalamus Visual IDE

```csharp
var manifest = bridge.GenerateManifest("MyGame", "1.0.0", registry, tables, graphs);
var json = manifest.ToJson();
// Send to Thalamus for Security Dashboard visualization
```

---

## Test Suite Expansion

**Added 14 new governance & fingerprint tests:**

- Fingerprint determinism (same input = same hash)
- Fingerprint uniqueness (different weights = different hash)
- Governance status transitions
- Audit log prefix correctness
- ProjectManifest JSON serialization
- Integration with surfaces & behavior graphs

**Total: 131 tests passing** (100 + 31 new).

---

## Technical Highlights

### Determinism Guarantee

Fingerprinting is deterministic across:
- ✅ netstandard2.1 (Unity, Mono)
- ✅ net8.0 (Modern .NET)
- ✅ Different runtimes (Windows, macOS, Linux)

Uses:
- `SHA256.Create() / ComputeHash()` (available on both targets)
- `StringComparer.Ordinal` for sorting
- `double.ToString("R")` for platform-independent precision
- `\x1F` (Unit Separator) as hash input delimiter

### Zero New Dependencies

- No new NuGet packages
- `System.Security.Cryptography` is BCL (built-in)
- `System.Text.Json` already in v0.5.1 dependencies

### Logging & Audit

- Governance status syncs automatically when certificate is set
- Thread-safe (uses existing `AuditLogger` locking)
- Backward compatible with `HasCode()` searches

---

## Release Artifacts

- **NuGet Package:** `BridgeMod.SDK.0.6.0.nupkg`
- **Target Frameworks:** `net8.0`, `netstandard2.1`
- **Package Tags:** `modding;gamedev;security;sovereign-tech;determinism;unity;godot;dotnet;...`
- **License:** MIT

---

## Known Limitations & Future Work

**Phase 6 (Planned):**
- Asset pipeline for texture/mesh/audio mods
- Cloud validation service (optional)
- Enhanced Thalamus integration hooks

**Current Scope:**
- Logic fingerprinting is read-only (no real-time updates to manifest)
- Governance status is set manually (no automatic sync with Kanon)
- Audit log prefixes are not cryptographically signed (that's Kanon's job)

---

## Getting Started with v0.6.0

### Install from NuGet

```bash
dotnet add package BridgeMod.SDK --version 0.6.0
```

### Generate a Manifest

```csharp
using BridgeMod.Bridge;
using BridgeMod.Bridge.Models;
using BridgeMod.Bridge.Procedural;

var config = new BridgeConfig { MaxStatValue = 9999 };
var bridge = new ModBridge(config);

// Register your surfaces, weight tables, behavior graphs...
var registry = new ModSurfaceRegistry();
registry.Register(new ModSurfaceDeclaration(
    "WeaponBalance", ModSurfaceCategory.Data, ModSurfaceStatus.Enabled, "Weapon stats"));

// Generate manifest
var manifest = bridge.GenerateManifest("MyRPG", "1.0.0", registry: registry);
Console.WriteLine(manifest.ToJson());
```

### Export for Thalamus

```csharp
var json = manifest.ToJson();
System.IO.File.WriteAllText("security_dashboard.json", json);
// Upload to Thalamus visual IDE
```

---

## Credits

BridgeMod v0.6.0 is maintained by the **DreamCraft: Legacies Project** with contributions from the community.

**Featured Partner:** Project Thalamus (DreamCraft Studio Suite)

---

## Support

- **GitHub:** https://github.com/rootedresilientshop-pixel/BridgeMod
- **Issues:** Report bugs at GitHub Issues
- **Discussions:** Join the community at GitHub Discussions
- **License:** MIT — free for all uses

---

**Thank you for using BridgeMod.** Let's make modding safer and more deterministic, one game at a time. 🌉
