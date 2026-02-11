# Decisions

This file records implicit decisions already reflected in code and repository structure.

## Product/Architecture Decisions Already Embedded

- Mods are package-based (`.zip`) with a required `manifest.json`.
- Runtime prioritizes local validation before load.
- Mods are treated as untrusted input with failure containment and disable-on-error behavior.
- Public API exposes explicit mod surface declaration by game developers.
- Supported mod categories are represented as:
  - Data
  - BehaviorGraph
  - Procedural
- Schema-first validation is used rather than inferred shape acceptance.

## Technical/Implementation Decisions Already Embedded

- JSON stack is `Newtonsoft.Json`.
- Unit tests use xUnit.
- SDK currently targets `net10.0` in csproj files.
- SDK package metadata is present in `sdk/BridgeMod.SDK.csproj` (NuGet-oriented packaging intent).
- Tool projects are present as executable scaffolds and reference the SDK.

## Repository/Process Decisions Already Embedded

- Governance/community files are part of the project surface (`CONSTITUTION.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`).
- GitHub automation and templates are configured (`.github/workflows/build.yml`, issue/PR templates).
- Root-level documentation is currently broad (product + status + release + community strategy mixed together).

## Decisions That Appear Unresolved

- Supported runtime baseline: `.NET 6+` (documentation claim) vs `net10.0` (implementation).
- Whether tool projects are expected to ship now or remain scaffolds.
- Which documents are authoritative when conflicting claims appear.