# Changelog

All notable changes to BridgeMod are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.1] - 2026-02-14

### Discovery Update

v0.2.1 improves how developers find BridgeMod. No new features or breaking API changes.

#### Improved

- NuGet package tags expanded for broader search discovery (game-modding, mod-loader, ugc, console-modding, and more)
- Package description updated with engine-specific keywords and clearer value proposition
- GitHub repository topics added for improved discoverability
- GitHub Discussions enabled for community engagement (Q&A, Ideas, Showcase, General)
- GitHub Sponsors support added via FUNDING.yml

#### No Breaking Changes

- Fully backward compatible with v0.2.0 and v0.1.0
- No API changes — this is a metadata and community infrastructure update

## [0.2.0] - 2026-02-11

### Confidence Update

v0.2.0 focuses on build reliability, documentation accuracy, and project hygiene. No new features or breaking API changes from v0.1.0.

#### Fixed

- Framework alignment: Solution and CI now target .NET 10.0 consistently (previously net10.0 in repo, net8.0 in published package)
- Solution build: Added stub Program.cs files to tool projects (ModPackager, SchemaValidatorCLI) to resolve CS5001 errors
- CI workflow: Updated to target .NET 10.0 x and run dotnet test at solution level
- VS Code launch configuration: Replaced stale net6.0 debug config with working "Run Tests" configuration
- Documentation: Removed references to unimplemented APIs (`GetContentAsJson`, `ValidateFileAccess`, `EnforceExecutionTimeout`)
- Documentation: Replaced phantom API usage with correct methods (`GetFile`, `ValidateFilePath`, `ValidateExecutionTime`)
- Platform version claims: Updated docs from ".NET 6.0+" to ".NET 10.0+" for consistency
- Nullable reference warnings: Fixed CS8620 warnings in ModSchema.cs with proper null filtering
- XML documentation warnings: Added comprehensive XML doc comments to all public members

#### Changed

- Package version: Updated to 0.2.0
- Package description: Updated to reference ".NET 10.0+" instead of ".NET 6.0+"
- Package release notes: Added detailed v0.2.0 release information
- NuGet package embedding: README.md now embedded in NuGet package for better discoverability
- Repository organization: Internal planning documents moved to `docs/internal/` with dedicated README
- Documentation links: Updated cross-references to point to new docs/internal/ location

#### Dependencies

- No changes to dependencies
- Continues to support .NET 10.0+
- Newtonsoft.Json 13.0.3 (unchanged)

#### Notes

- All 26 unit tests passing
- Zero compiler warnings in Release build
- Full backward compatibility maintained

---

## [0.1.0] - 2026-02-05

### Phase 1: Foundations - Complete Release

BridgeMod v0.1.0 is the foundation of safe, transparent modding for console and PC.

#### Added

**Core SDK Components:**
- `ModManifest.cs` - Manifest parsing with semantic versioning and dependency validation
- `ModSchema.cs` - Comprehensive field validation, type checking, and behavior graph constraints
- `ModValidator.cs` - Package validation, manifest integrity checks, and content verification
- `ModLoader.cs` - Safe mod loading with automatic error recovery and mod disabling
- `ExecutionGuards.cs` - Sandbox execution with timeouts, file access validation, and parameter bounds
- `ModSurfaceDeclaration.cs` - Public API for developers to declare what modders can modify

**Features:**
- Three mod types: Pure Data, Behavior Graphs, Procedural Control
- Manifest validation with detailed error reporting
- Field constraints: String length, numeric bounds, array sizes
- Behavior graph validation (nodes, edges, depth limits)
- Execution timeouts (5 seconds default, configurable)
- Automatic mod disabling on errors (failure containment)
- Human-readable surface documentation generation
- JSON export of mod surface declarations

**Testing:**
- 30+ comprehensive unit tests
- Full test coverage of validation, loading, and execution guards
- Examples demonstrating all three mod types

**Documentation:**
- `CONSTITUTION.md` - Governing principles
- `MOD_SCHEMA.md` - Mod package format specification
- `README_DEVELOPMENT.md` - Full implementation guide
- `IMPLEMENTATION_STATUS.md` - Detailed architecture and design
- `console_modding_execution_plan.md` - Complete 5-phase roadmap
- `QUICKSTART.md` - 10-minute integration guide
- `CONTRIBUTING.md` - Contribution guidelines
- `CODE_OF_CONDUCT.md` - Community standards

**GitHub Infrastructure:**
- GitHub Actions workflows for build and test
- Issue and pull request templates
- Community health files
- NuGet package metadata

### Principles Implemented

✅ **Developer-First Authority** - Developers declare what can be modded via `ModSurfaceDeclaration`

✅ **Platform Transparency** - Auto-generated documentation shows modders exactly what surfaces are available

✅ **Safety by Design** - Mods treated as untrusted data, sandboxed execution prevents crashes

✅ **Hybrid Validation** - Local validation mandatory, cloud optional (future phase)

✅ **Failure Containment** - Mods automatically disabled on errors, game remains stable

✅ **Anonymous Distribution** - File-based, no accounts required, no moderation burden

### Known Limitations

- No asset replacement mods (v2+)
- No scripting support (v2+)
- No player-facing mod browser (v5+)
- No cloud validation services (v5+)
- No mod dependency resolution beyond version checking (v2+)

### Dependencies

- `.NET 10.0` or later
- `Newtonsoft.Json 13.0.3`

---

## Upcoming Releases

See [console_modding_execution_plan.md](console_modding_execution_plan.md) for the complete roadmap:

### Phase 2: Developer Mod Surfaces
- Enhanced `ModSurfaceDeclaration` features
- Surface versioning and evolution
- Capability matrix generation
- Surface dependency tracking

### Phase 3: Behavior Graph Runtime
- Deterministic graph executor
- Time and depth limit enforcement
- Debug logging and profiling

### Phase 4: Procedural Control Layer
- Seed and parameter validation
- Determinism enforcement
- Best practice documentation

### Phase 5: Optional Cloud Validation
- Cloud validation endpoint
- Metadata enrichment
- Community modding index

---

## How to Report Issues

- **Bugs:** Use [Bug Report](https://github.com/yourusername/BridgeMod/issues/new?template=bug_report.md)
- **Features:** Use [Feature Request](https://github.com/yourusername/BridgeMod/issues/new?template=feature_request.md)
- **Security:** Email maintainers privately

## How to Contribute

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for community standards.

---

**Want to help?** Check the [roadmap](console_modding_execution_plan.md) and open an issue. We're building this together.
