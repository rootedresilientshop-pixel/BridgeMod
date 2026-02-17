# BridgeMod Quickstart Guide

Get BridgeMod running in your game in 10 minutes.

## Prerequisites

- .NET 10.0 SDK or later
- Your game project (Unity, Godot, or any C# environment)
- A basic understanding of JSON

## Step 1: Add BridgeMod to Your Project

### Via NuGet (Recommended)

```bash
dotnet add package BridgeMod.SDK
```

### Or: Build from Source

```bash
git clone https://github.com/yourusername/BridgeMod.git
cd BridgeMod/sdk
dotnet build --configuration Release
# Reference BridgeMod.SDK.csproj in your project
```

## Step 2: Declare What Modders Can Modify

Create a mod surface declaration in your game's initialization:

```csharp
using BridgeMod.PublicAPI;

public class GameModSetup
{
    public void InitializeModding()
    {
        // Tell modders what can be modded
        var surfaces = new ModSurfaceDeclaration("MyAwesomeGame", "1.0.0");

        // Example: Let modders tweak game balance
        surfaces.DeclareDataSurface(
            name: "GameBalance",
            description: "Adjust weapon damage, armor values, and difficulty multipliers",
            defaultPath: "data/balance.json"
        );

        // Example: Let modders create behavior graphs (AI, state machines)
        surfaces.DeclareBehaviorGraphSurface(
            name: "EnemyAI",
            description: "Define enemy decision logic using state graphs",
            defaultPath: "graphs/enemy-ai.graph.json"
        );

        // Example: Let modders control procedural generation
        surfaces.DeclareProcedurtalSurface(
            name: "WorldGeneration",
            description: "Control world seed and generation parameters",
            defaultPath: "procedural/worldgen.json"
        );

        // Export this for modders to see
        string documentation = surfaces.GenerateSurfaceSummary();
        System.IO.File.WriteAllText("MOD_SURFACES.md", documentation);

        LoadModsFromDirectory("./mods");
    }
}
```

## Step 3: Load Mods Safely

```csharp
using BridgeMod.Runtime;

public void LoadModsFromDirectory(string modDirectory)
{
    var validator = new ModValidator();
    var loader = new ModLoader(validator);

    foreach (var modFile in Directory.GetFiles(modDirectory, "*.zip"))
    {
        var mod = loader.LoadMod(modFile);

        if (mod?.IsEnabled == true)
        {
            // Use the mod
            ApplyMod(mod);
            Debug.Log($"Loaded mod: {mod.Name} v{mod.Version}");
        }
        else
        {
            Debug.LogWarning($"Mod {modFile} failed to load: {mod?.DisableReason}");
        }
    }
}
```

## Step 4: Execute Mod Logic Safely

Wrap any mod execution with guards to prevent crashes:

```csharp
using BridgeMod.Runtime;

public void ApplyMod(LoadedMod mod)
{
    var guards = new ExecutionGuards();

    try
    {
        // Create execution context with timeout enforcement
        using (var context = guards.CreateExecutionContext(mod.Name, 5000))
        {
            // Your game's logic to apply the mod
            var balanceDataJson = mod.GetFile("data/balance.json");
            if (balanceDataJson != null)
            {
                var balanceData = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(balanceDataJson);
                ApplyBalanceChanges(balanceData);
            }

            context.Complete();
        }
    }
    catch (Exception ex)
    {
        loader.HandleModError(mod.Name, ex);
        // Mod is automatically disabled
        Debug.LogError($"Mod {mod.Name} encountered an error and was disabled");
    }
}
```

## Step 5: What Your Modders Will See

When you export `MOD_SURFACES.md`, modders get this:

```markdown
# MyAwesomeGame Mod Surfaces

## GameBalance (Data Mod)
Adjust weapon damage, armor values, and difficulty multipliers

**File:** data/balance.json
**Status:** Enabled
**Version:** 1.0.0

### Example Mod Structure
```
my-balance-mod.zip
├── manifest.json
└── data/
    └── balance.json
```

## Creating Your First Mod

### 1. Create the manifest.json

```json
{
  "name": "better-balance",
  "version": "1.0.0",
  "author": "YourName",
  "description": "Rebalances weapons for PvP",
  "modType": "Data",
  "gameVersion": "1.0.0",
  "dependencies": [],
  "files": ["data/balance.json"]
}
```

### 2. Create your data file (data/balance.json)

```json
{
  "weapons": {
    "sword": {
      "damage": 25,
      "cost": 100
    },
    "bow": {
      "damage": 15,
      "cost": 75
    }
  }
}
```

### 3. Package as ZIP

Your mod structure:
```
my-balance-mod.zip
├── manifest.json
└── data/
    └── balance.json
```

### 4. Test Locally

Drop the .zip into your game's `mods/` folder and test it.

## Key Principles

✅ **You're in control.** You decide what can be modded.

✅ **Modders know the rules.** Exported documentation shows exactly what's possible.

✅ **Mods can't break your game.** Validation and sandboxing ensure mod errors are isolated.

✅ **Local validation only.** Cloud services are optional (Phase 2+).

## Next Steps

- **Full API Reference:** See [README_DEVELOPMENT.md](README_DEVELOPMENT.md)
- **Mod Schema Details:** See [MOD_SCHEMA.md](MOD_SCHEMA.md)
- **Example Mods:** Check [examples/](examples/)
- **Ask Questions:** Open an issue on GitHub

## Troubleshooting

### "Mod failed to load: Invalid manifest"
Your manifest.json is missing required fields. Check against the example above.

### "Mod is disabled"
Check your game's logs. The mod likely failed validation or threw an exception.

### "My mod data isn't being applied"
Make sure your filenames match the `defaultPath` you declared in your surface.

---

**Questions?** Open an issue or check [CONTRIBUTING.md](CONTRIBUTING.md).

Welcome to safer modding. 🎮
