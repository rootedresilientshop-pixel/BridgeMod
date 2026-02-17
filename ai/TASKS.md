# Tasks: BridgeMod v0.2.4+ (Active & Planned)

## Completed (v0.2.0 - Confidence Update)

### ✅ Phase 1: Build Fixed
- [x] Update all csproj files to target net10.0 consistently
- [x] Create Program.cs stubs for ModPackager and SchemaValidatorCLI
- [x] Update CI workflow to .NET 10.0 with solution-level builds
- [x] Verify solution builds cleanly (0 errors)

### ✅ Phase 2: Documentation Fixed
- [x] Remove phantom API references from docs and examples
- [x] Replace with actual SDK methods (GetFile, ValidateFilePath, CreateExecutionContext)
- [x] Update version claims (.NET 6.0 → 10.0 everywhere)
- [x] Fix .vscode/launch.json launch configuration

### ✅ Phase 3: Repository Cleaned
- [x] Create docs/internal/ directory
- [x] Move internal planning docs to docs/internal/
- [x] Create docs/internal/README.md explaining folder purpose
- [x] Update cross-references to point to new location

### ✅ Phase 4: NuGet Improved
- [x] Update package version to 0.2.0
- [x] Update package description to reflect .NET 10.0+
- [x] Add PackageReadmeFile to embed README in package
- [x] Verify code samples compile against live API

### ✅ Phase 5: Compiler Warnings Fixed
- [x] Fix nullable reference warnings (CS8620) in ModSchema.cs
- [x] Add XML doc comments to all public members
- [x] Eliminate XML documentation warnings (CS1591)
- [x] Achieve zero warnings in Release build

### ✅ Phase 6: Release Prepared (v0.2.0)
- [x] Update CHANGELOG.md with v0.2.0 entry
- [x] Create comprehensive git commit
- [x] Tag v0.2.0 and push to remote
- [x] Create GitHub Release with detailed notes
- [x] Publish to NuGet

---

## ✅ Phase 1: Security Foundation — 100% COMPLETE (v0.2.4)

### ✅ Firewall Standard Architecture
- [x] Design and implement 3-gate C# validation pipeline (Type Check → Boundary Guards → Audit Logging)
- [x] Implement `BridgeConfig` for configurable boundary guards
- [x] Implement `AuditLogger` (in-memory, operational)
- [x] Verify script injection is blocked without crashing the Python engine
- [x] Verify out-of-bounds stats (999999+ health) are clamped

### ✅ Air-Gap Architecture
- [x] Isolate `DreamCraft.Engine` (Python) from raw mod data
- [x] Ensure Python Engine only consumes scrubbed JSON from C# SDK
- [x] Implement safe defaults in Python Engine when SDK blocks a mod

### ✅ Repository Overhaul (v0.2.4)
- [x] Move to professional `/src/` + `/samples/` layout
- [x] Replace legacy `/sdk/` with `/src/BridgeMod.SDK` as single source of truth
- [x] Create `/src/DreamCraft.Engine` (isolated Python core)
- [x] Create `/samples/Legacies_Bridge_Test` (end-to-end integration proof)
- [x] Publish BridgeMod.SDK v0.2.4 to NuGet and verify NuGet sync

---

## Active (Phase 2 — Schema Registry)

### 🔥 Schema Registry (TOP PRIORITY — Phase 2 Start)
- [ ] Design schema registry architecture: how Python engine definitions map to C# validation rules
- [ ] Build a tool to auto-generate C# validation rules from Python engine definitions
- [ ] Expand `AuditLogger` to support external file export (write audit logs to disk)
- [ ] Write tests covering schema registry validation paths
- [ ] Update docs to reflect new Schema Registry tooling

### 📊 Gathering Feedback (In Progress)
- [ ] Post on Reddit (r/csharp, r/gamedev, r/Unity, r/godot)
- [ ] Set up GitHub Discussions with categories:
  - [ ] Show & Tell
  - [ ] Questions
  - [ ] Ideas / Feature Requests
  - [ ] Showcase (games using BridgeMod)
- [ ] Track metrics: downloads, GitHub stars, issues, engagement
- [ ] Monitor for "We're using this in production" feedback

### 🎯 Community Engagement (In Progress)
- [ ] Respond to GitHub issues and Discussions
- [ ] Track which features/pain points come up repeatedly
- [ ] Identify game studios/developers using BridgeMod
- [ ] Document real-world use cases

---

## Pending: Phase 2 Remainder (After Schema Registry)

### 🔄 Enhanced Mod Surfaces
- [ ] Design surface versioning system
- [ ] Implement capability matrix generation
- [ ] Add surface dependency tracking
- [ ] Improve documentation discovery for modders
- [ ] Create v0.3.0 release

### 📋 Expected Work
- [ ] Add versioning to ModSurfaceDeclaration
- [ ] Generate capability matrix JSON
- [ ] Update examples to show advanced surface features
- [ ] Run 26/26 tests; verify zero warnings

### 📊 Go/No-Go Decision Point
- **Go if:** 20+ downloads/week AND at least 3 "we're using this" confirmations
- **No-go if:** Downloads trending downward AND no community engagement
- **Timeline:** Review after 1 month of v0.2.4 being live

---

## Planned: Phase 3 (Behavior Graphs - Future)

### 🎮 Behavior Graph Runtime
- [ ] Design deterministic graph executor
- [ ] Implement time limit enforcement per graph execution
- [ ] Add depth limit enforcement
- [ ] Add debug logging and profiling
- [ ] Create state machine validation
- [ ] Write 20+ tests for graph execution
- [ ] Document graph format and limitations
- [ ] Create v0.4.0 release

### 🎯 Key Decisions Needed
- Execution model (step-based vs. tick-based)
- Time budget per execution
- Node type library (decision nodes, state nodes, action nodes)
- Graph complexity limits

---

## Planned: Phase 4 (Procedural Control - Future)

### 🌍 Procedural Generation Parameters
- [ ] Design parameter validation schema
- [ ] Implement seed validation
- [ ] Add generation bounds checking
- [ ] Create v0.5.0 release

---

## Planned: Phase 5 (Cloud Services - Future)

### ☁️ Optional Cloud Validation
- [ ] Design cloud validation service (optional)
- [ ] Implement opt-in telemetry (privacy-first)
- [ ] Create v0.6.0+ release

---

## Ongoing (All Phases)

### 📚 Documentation Maintenance
- [ ] Keep docs/examples in sync with SDK API
- [ ] Review docs for accuracy on every release
- [ ] Respond to documentation issues in GitHub

### 🧪 Test Coverage
- [ ] Maintain 26+ tests passing for v0.2.0
- [ ] Add tests for Phase 2, 3, 4 features as they're implemented
- [ ] Verify zero warnings in Release builds

### 🎮 Engagement
- [ ] Monitor GitHub Discussions
- [ ] Respond to issues within 48 hours
- [ ] Track real-world use cases
- [ ] Celebrate public projects using BridgeMod

---

## Decision Points

### After 1 Month (March 15, 2026)
**Question:** Is there evidence of real adoption?

**Metrics to Check:**
- Downloads/week: trending up or down?
- GitHub issues: any real usage problems?
- Reddit engagement: positive or silent?
- "Using in production" testimonials: any?

**Decision:**
- ✅ **Proceed to Phase 2** if: 20+ downloads/week + community engagement
- ⏸️ **Iterate v0.2.x** if: slow downloads but solid technical interest
- 🔄 **Reassess positioning** if: declining downloads + no engagement

### After Phase 2 (Post-v0.3.0)
**Question:** Do enhanced surfaces unlock new use cases?

**Metric:** Same as above, but with higher thresholds

**Decision:**
- ✅ **Proceed to Phase 3** if: clear feature requests for behavior graphs
- ⏸️ **Extend Phase 2** if: still building v0.3.0 adoption

---

## Blocked/At Risk (None Currently)

v0.2.0 cleared all blockers:
- ✅ Build system fixed
- ✅ Documentation accurate
- ✅ Compiler warnings eliminated
- ✅ Released to GitHub and NuGet

---

## Backlog: If Adoption Accelerates

If BridgeMod gains rapid adoption, priority order:

1. Improve ModPackager tool (currently stub)
2. Improve SchemaValidator tool (currently stub)
3. Create example end-to-end project (repo + runnable)
4. Add behavior graph support (Phase 3)
5. Add monetization/sponsorship pathway

---

## Success Criteria (v0.2.0)

- ✅ Zero build errors, zero compiler warnings
- ✅ 26/26 tests passing
- ✅ All public members documented
- ✅ GitHub Release published
- ✅ NuGet package live
- ⏳ Waiting: Real developer feedback
- ⏳ Waiting: Adoption metrics
