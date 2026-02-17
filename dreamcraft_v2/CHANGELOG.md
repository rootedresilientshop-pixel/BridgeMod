# Changelog

All notable changes to BridgeMod.sdk are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.2.3] — 2026-02-17

### Added

#### Formal MIT License
- `LICENSE` file added to repository root — required for NuGet.org publication
  and open-source compliance. License expression `MIT` is now declared in the
  `<PackageLicenseExpression>` field of `BridgeMod.SDK.csproj`.

#### Firewall Architecture — Public API Promotion
- All bridge types (`ModBridge`, `BridgeConfig`, `AuditLogger`, `ValidationResult`,
  `ErrorCodes`) promoted from `internal` to `public` and extracted into a dedicated
  library source file `BridgeMod.Bridge.cs`. Previously these lived as `internal`
  classes inside the sample runner, making them inaccessible to NuGet consumers.
- `BridgeMod.Bridge.cs` is now the single, authoritative public API surface of the SDK.

#### Multi-Target Framework Support
- SDK now ships two TFM builds in the `.nupkg`:
  - `net8.0` — .NET LTS, for server and standalone console use.
  - `netstandard2.1` — broadest Unity/Mono compatibility for game engine integration.
- `IsExternalInit.cs` polyfill added to enable C# 9+ `record` and `init` semantics
  on the `netstandard2.1` target without runtime breakage.

#### Project Split: Library vs. Sample
- `BridgeMod.SDK.csproj` — Class Library (NuGet package). Contains only the public
  bridge API. Does not include the sample runner.
- `Legacies_Bridge_Test/Legacies_Bridge_Test.csproj` — Console App sample that
  references the library via project reference. Demonstrates all three validation
  gates with annotated code and live output.

#### README Embedded in NuGet Package
- `README.md` is now included in the `.nupkg` via `PackageReadmeFile`, so the
  architecture diagram and verification table are displayed directly on the
  NuGet.org package page.

### Changed

- `BridgeMod.SDK.csproj` version bumped `0.2.2 → 0.2.3` to publish the Firewall
  architecture and MIT license as the authoritative public release.

---

## [0.2.2] — 2024-02-16

### Added

#### Audit Logging
- Introduced `AuditLogger` (C#) and `AuditLogger` (Python) — append-only event
  recorders that write a tamper-evident entry for every validation event.
- Two initial event codes:
  - `PARSE_ERR_001`: Payload contained disallowed markup (HTML/script tags) in a
    string field. Logged and the payload is rejected in full.
  - `BOUND_CLAMP_003`: A numeric stat field exceeded its boundary and was clamped
    to the allowed maximum. Logged; payload is still accepted with the corrected value.
- Audit entries include ISO 8601 UTC timestamp, event code, payload ID, and a
  human-readable detail string.

#### Boundary Guards
- Added `BoundaryGuards` pipeline stage (Gate 2) to both C# `ModBridge` and Python
  `BridgeValidator`.
- Guarded stat fields: `health`, `mana`, `strength`, `defense`, `speed`.
- Values are clamped to `[0, 9999]` — matching the DreamCraft: Legacies engine's
  safe operating range for all character stat types.
- Clamping is non-destructive: the sanitized payload is still returned as valid,
  with the corrected value, so legitimate mods with high-but-safe values pass through.

#### Sample Folder: `Legacies_Bridge_Test/`
- `Program.cs` — Annotated C# sample runner demonstrating all three validation gates.
- `pulse_test.py` — pytest suite with three canonical test cases (Standard, Malicious, Bounds).
- `Sample_Walkthrough.md` — Step-by-step tutorial for developers integrating the SDK.

#### Project Documentation
- `README.md` rewritten with Shields.io badges, architecture diagram, and
  Verification Summary table.
- `console_modding_execution_plan.md` added — full integration roadmap and Unity guide.
- `CHANGELOG.md` (this file) introduced.

### Changed

- `ModBridge.Validate()` now returns a `ValidationResult` record instead of a raw
  boolean. `ValidationResult` exposes `IsValid`, `SanitizedPayload`, and `ErrorCode`.
- Error handling in the string-sanitization gate changed from silent stripping to
  full payload rejection. This is a **breaking change** from 0.2.1 — callers that
  expected malformed-but-stripped payloads to pass will now see `IsValid: false`.

### Fixed

- Stat fields stored as `double` were previously compared using `==`, which could
  produce false negatives due to floating-point precision. Comparison now uses
  `Math.Abs(clamped - value) > double.Epsilon`.

---

## [0.2.1] — 2024-01-20

### Added
- Initial C# `ModBridge` class with basic JSON deserialization.
- Python `BridgeValidator` stub with structural schema check.

### Known Issues
- No boundary guards — numeric overflow not prevented.
- No audit logging — validation events are silent.
- String sanitization strips tags silently rather than rejecting.

  *(All three issues resolved in 0.2.2.)*

---

## [0.2.0] — 2024-01-05

### Added
- Project scaffold: FastAPI simulation engine, aiosqlite persistence layer,
  LLM client (Ollama-compatible), Docker deployment.
- Initial BridgeMod concept documented in `decisions.md`.

---

*For the full integration roadmap, see [console_modding_execution_plan.md](console_modding_execution_plan.md).*
