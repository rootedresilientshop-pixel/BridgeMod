# Decisions

This file documents key decisions—both architectural (embedded in code) and process (guiding v0.2.0).

## Product/Architecture Decisions (Embedded in Code)

- Mods are package-based (`.zip`) with required `manifest.json`
- Runtime prioritizes local validation before load (no cloud prerequisite)
- Mods treated as untrusted input with failure containment and disable-on-error behavior
- Public API exposes explicit mod surface declaration by game developers
- Supported mod categories: Data, BehaviorGraph, Procedural
- Schema-first validation (not inferred shape acceptance)
- **NEW (v0.2.0):** Data mods are sandbox-safe for console deployment

## Technical/Implementation Decisions (Embedded in Code)

- JSON stack: `Newtonsoft.Json 13.0.3`
- Unit testing: xUnit
- **NEW (v0.2.0):** SDK targets `net10.0` (aligned with published package)
- Tool projects now include minimal CLI stubs (`Program.cs`) with usage messaging
- Package metadata in `sdk/BridgeMod.SDK.csproj` with NuGet-oriented intent
- **NEW (v0.2.0):** All public members have XML documentation (`GenerateDocumentationFile=true`)
- **NEW (v0.2.0):** Nullable reference warnings fixed across SDK

## Repository/Process Decisions (Guiding v0.2.0)

- Governance/community files are part of public surface (`CONSTITUTION.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`)
- GitHub automation configured (`.github/workflows/build.yml`, issue/PR templates)
- **NEW (v0.2.0):** Internal planning docs moved to `docs/internal/` (keep root focused on product)
- **NEW (v0.2.0):** README embedded in NuGet package via `PackageReadmeFile`

## Framework Support Decision (v0.2.0)

**Decision:** Target `net10.0` consistently across repo and published package.

**Rationale:**
- Repo originally targeted `net10.0` but published package claimed `net8.0`
- Creates confusion and incompatibility
- v0.2.0 aligns: SDK targets `net10.0`, package targets `net10.0`, CI tests `net10.0 x`
- Forward-compatible with older code; .NET 10 is current LTS ecosystem

**Implication:** Developers need .NET 10.0+ to use BridgeMod. Xbox/PlayStation support verified on .NET 10 compatible SDKs.

## Documentation Strategy Decision (v0.2.0)

**Decision:** Remove phantom API references, verify all examples against live SDK surface, update version claims.

**Phantom APIs Removed:**
- `GetContentAsJson()` → replaced with `GetFile()` + manual JSON parsing
- `ValidateFileAccess()` → replaced with `ValidateFilePath()`
- `EnforceExecutionTimeout()` → replaced with `CreateExecutionContext()` + timeout management

**Rationale:** Docs should describe what actually exists, not planned features. Phantom references undermine trust and cause developer frustration.

## Release & Distribution Decision (v0.2.0)

**Decision:** Publish both GitHub Release and NuGet package simultaneously.

**Strategy:**
- v0.2.0 tag pushed to origin
- GitHub Release created with detailed notes and binary links
- NuGet package published (0.2.0) with embedded README

**Why:** Gives developers options (GitHub watchers, NuGet searchers, npm-like experience).

## Repository Cleanup Decision (v0.2.0)

**Decision:** Move internal/planning docs out of root; keep root focused on product docs.

**Moved to `docs/internal/`:**
- `SETUP_COMPLETE.md`
- `IMPLEMENTATION_STATUS.md`
- `GITHUB_PREP_SUMMARY.md`
- `GITHUB_RELEASE_CHECKLIST.md`
- `DISCUSSIONS_STRATEGY.md`
- `CROWDFUNDING.md`
- `console_modding_execution_plan.md`

**Added:** `docs/internal/README.md` explaining folder purpose

**Rationale:** Keeps repository root uncluttered and signals what's product-facing vs. internal operations.

## Validation & Testing Decision (v0.2.0)

**Decision:** Ensure zero compiler warnings and 100% XML documentation coverage in Release build.

**Implemented:**
- Fixed CS8620 (nullable reference) warnings in `ModSchema.cs`
- Added comprehensive XML comments to all public members
- CI now tests Release configuration with warning-as-error disabled but documented

**Rationale:** Builds confidence that code is production-ready and maintainable.

## Unresolved Decisions (For Future Phases)

1. **Phase 3 Behavior Graphs:** Will BridgeMod provide graph executor or expect games to implement? (Planned to implement)
2. **Phase 4 Procedural:** How strict are bounds checking? (Planned: very strict, fail-safe)
3. **Phase 5 Cloud:** Optional or default? (Planned: optional, local-first)
4. **Monetization:** When/how to monetize (pending developer feedback)
5. **Tool Projects:** When/how to implement `ModPackager` and `SchemaValidator` (pending Phase 2+ feature clarity)
