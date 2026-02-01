# BridgeMod Development Guide

## Building the Project

### Prerequisites
- .NET 6.0 SDK or later
- Visual Studio 2022 / Visual Studio Code

### Build from Command Line

```bash
cd BridgeMod
dotnet build
```

### Build for Release

```bash
dotnet build --configuration Release
```

### Running Tests

```bash
dotnet test
```

## Project Structure

```
BridgeMod/
├── sdk/
│   ├── Runtime/
│   │   ├── ModValidator.cs      # Validates mod packages locally
│   │   ├── ModLoader.cs         # Loads validated mods
│   │   └── ExecutionGuards.cs   # Failure containment & safety
│   │
│   ├── Data/
│   │   ├── ModManifest.cs       # Manifest parsing & versioning
│   │   └── ModSchema.cs         # Schema validation
│   │
│   ├── PublicAPI/
│   │   └── ModSurfaceDeclaration.cs  # Public API for developers
│   │
│   └── BridgeMod.SDK.csproj
│
├── tests/
│   ├── Phase1Tests.cs            # Comprehensive unit tests
│   └── BridgeMod.Tests.csproj
│
├── tools/
│   ├── ModPackager/              # Tool to create mod packages
│   └── SchemaValidatorCLI/       # CLI for schema validation
│
├── examples/
│   └── UnitySampleGame/          # Sample Unity integration
│
└── docs/
    ├── vision.md
    ├── mod-surfaces.md
    └── xbox-compliance.md
```

## Phase 1: Foundations (Complete)

Implemented safe mod loading without cloud dependency.

### Core Components

#### 1. **ModManifest.cs**
- Parses and validates mod manifest files
- Semantic versioning support
- Dependency declaration and validation

```csharp
var manifest = new ModManifest
{
    Name = "MyMod",
    Version = "1.0.0",
    Author = "ModAuthor",
    ModType = ModType.Data,
    Files = new[] { "data/config.json" }
};

if (manifest.IsValid(out var errors))
{
    // Manifest is valid
}
```

#### 2. **ModSchema.cs**
- Defines validation rules for mod content
- Type checking (String, Number, Boolean, Array, Object)
- Boundary enforcement (min/max values, max length)
- Behavior graph validation with node/depth limits

#### 3. **ModValidator.cs**
- Validates complete mod packages (.zip files)
- Checks manifest integrity
- Validates all declared files exist
- Validates mod contents against registered schemas

```csharp
var validator = new ModValidator();
var result = validator.ValidateModPackage("mod.zip");

if (result.IsValid)
{
    Console.WriteLine("Mod is safe to load");
}
else
{
    foreach (var error in result.Errors)
        Console.WriteLine($"Error: {error}");
}
```

#### 4. **ModLoader.cs**
- Loads validated mods into memory
- Tracks loaded mods by name
- Automatic error handling and mod disabling
- File content extraction from mod packages

```csharp
var loader = new ModLoader(validator);
var mod = loader.LoadMod("mod.zip");

if (mod != null && mod.IsEnabled)
{
    // Use the loaded mod
    var fileContent = mod.GetFile("data/config.json");
}
```

#### 5. **ExecutionGuards.cs**
- Isolates mod failures from game crashes
- Enforces execution time limits (5 seconds default)
- Validates file access stays within sandbox
- Parameter bounds validation
- Tracks disabled mods with reasons

```csharp
var guards = new ExecutionGuards();

// Create isolated execution context
var context = guards.CreateExecutionContext("MyMod", timeoutMs: 5000);

try
{
    // Execute mod code
    ExecuteModLogic();
    context.Complete();
}
catch (Exception ex)
{
    context.RecordError(ex);
    guards.DisableMod("mod.zip", ex.Message);
}
```

#### 6. **ModSurfaceDeclaration.cs** (Public API)
- Game developers declare what modders can modify
- Supports 3 mod types: Data, BehaviorGraph, Procedural
- Generates human-readable surface documentation
- Tracks surface status (Enabled, Limited, Disabled, Planned)

```csharp
var declaration = new ModSurfaceDeclaration("MyGame", "1.0.0");

declaration.DeclareDataSurface(
    name: "GameBalance",
    description: "Weapon and enemy balance values",
    filePath: "data/balance.json",
    status: SurfaceStatus.Enabled);

declaration.DeclareBehaviorGraphSurface(
    name: "NPC AI",
    description: "NPC behavior trees",
    filePath: "graphs/npc_ai.graph.json",
    maxNodeCount: 500,
    maxGraphDepth: 20);

// Generate documentation
var summary = declaration.GenerateSurfaceSummary();
File.WriteAllText("MOD_SURFACES.md", summary);
```

## Mod Package Format (v1)

### Manifest (manifest.json)

```json
{
  "name": "WeaponBalance",
  "version": "1.0.0",
  "author": "CommunityModder",
  "description": "Rebalances weapons for PvP",
  "modType": "data",
  "files": ["data/weapons.json"],
  "dependencies": ["GameCore@1.0.0"],
  "tags": ["balance", "weapons"]
}
```

### Package Structure

```
WeaponBalance.zip
├── manifest.json
├── data/
│   └── weapons.json
├── graphs/
│   └── (empty for data mods)
└── procedural/
    └── (empty for data mods)
```

### Data Mod Example

```json
{
  "type": "weaponConfig",
  "content": {
    "weapons": [
      {
        "id": "sword",
        "damage": 25,
        "speed": 1.0
      }
    ]
  }
}
```

### Behavior Graph Mod Example

```json
{
  "id": "npc_state_machine",
  "nodes": [
    {"id": "idle", "type": "state"},
    {"id": "patrol", "type": "state"},
    {"id": "combat", "type": "state"}
  ],
  "edges": [
    {"from": "idle", "to": "patrol", "condition": "nearby_player=false"},
    {"from": "patrol", "to": "combat", "condition": "player_detected=true"}
  ]
}
```

## Integration Example

```csharp
// In your game's initialization
var validator = new ModValidator();
var loader = new ModLoader(validator);

// Declare what modders can modify
var surfaceDecl = new ModSurfaceDeclaration("MyGame", "1.0.0");
surfaceDecl.DeclareDataSurface("Balance", "Game balance", "data/balance.json");
surfaceDecl.DeclareBehaviorGraphSurface("AI", "NPC AI", "graphs/npc_ai.graph.json");

// Load mods from directory
var modsDir = "Assets/Mods";
foreach (var modFile in Directory.GetFiles(modsDir, "*.zip"))
{
    var mod = loader.LoadMod(modFile);
    if (mod?.IsEnabled == true)
    {
        ApplyMod(mod);
    }
}
```

## Testing

Run the comprehensive test suite:

```bash
dotnet test --logger:"console;verbosity=detailed"
```

Tests cover:
- Manifest parsing and validation
- Semantic versioning and compatibility
- Schema validation (all field types)
- Behavior graph constraints
- Execution guards and isolation
- Mod surface declaration

## Safety Guarantees (v1)

✅ **Data-Only Mods**: No executable code from mods
✅ **Local Validation**: All checks performed locally, cloud optional
✅ **Sandboxed Execution**: Mods cannot access game files outside mod directory
✅ **Time Limits**: 5-second timeout per mod operation
✅ **Automatic Disabling**: Mods that fail are automatically disabled
✅ **Deterministic**: Behavior graphs are deterministic and repeatable
✅ **Console-Safe**: Designed to pass Xbox certification

## Next Steps (Phase 2)

- ModSurfaceDeclaration enhancements
- Capability matrix generation
- Surface status management UI
- Cloud validation service (optional)

## Contributing

Follow the Constitution.md principles:
1. **Developer-First**: Developers control mod surfaces
2. **Platform Transparency**: Mods are clearly declared
3. **Safety by Design**: Mods treated as untrusted data
4. **Hybrid Validation**: Local validation mandatory
5. **Failure Containment**: Errors isolated from game
6. **Anonymous Distribution**: v1 is file-based

See CONSTITUTION.md for full governance guidelines.
