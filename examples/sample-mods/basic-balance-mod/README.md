# Basic Balance Adjustment Mod

This is a complete, working example of a **BridgeMod v1 Data Mod**.

## What This Mod Does

It adjusts game balance by modifying:
- Weapon damage, attack speed, and cost
- Difficulty multipliers for enemy damage and player health

## File Structure

```
basic-balance-mod/
├── manifest.json          # Mod metadata
└── data/
    └── balance.json       # The actual balance data
```

## How to Use This

### 1. Package as ZIP

```bash
# Create a ZIP file with this structure
basic-balance-mod.zip
├── manifest.json
└── data/
    └── balance.json
```

### 2. Drop Into Your Game

Place `basic-balance-mod.zip` in your game's `mods/` folder.

### 3. Your Game Loads It

```csharp
var loader = new ModLoader(new ModValidator());
var mod = loader.LoadMod("basic-balance-mod.zip");

if (mod?.IsEnabled == true)
{
    var balanceDataJson = mod.GetFile("data/balance.json");
    if (balanceDataJson != null)
    {
        var balanceData = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(balanceDataJson);
        ApplyBalanceChanges(balanceData);
    }
}
```

## What Makes This Valid

✅ `manifest.json` has all required fields
✅ `modType` is "Data" (v1 supported)
✅ Files listed in manifest actually exist
✅ `data/balance.json` is valid JSON
✅ No scripts, no executables, just data

## Modifying This Example

You can change any values in `balance.json`:
- Increase `damage` to make weapons stronger
- Adjust `cost` to change progression
- Modify multipliers for difficulty changes
- Add new weapons or difficulties

The structure doesn't matter—what matters is that your **game expects this structure** and declares it via `ModSurfaceDeclaration`.

## Next Steps

1. **Look at** [QUICKSTART.md](../../QUICKSTART.md) for integration steps
2. **Read** [MOD_SCHEMA.md](../../MOD_SCHEMA.md) for detailed format
3. **Check** [README_DEVELOPMENT.md](../../README_DEVELOPMENT.md) for API reference

## Questions?

- How do I create a behavior graph mod? → See [console_modding_execution_plan.md](../../console_modding_execution_plan.md) (Phase 3)
- How do I declare my game's mod surfaces? → See [QUICKSTART.md](../../QUICKSTART.md)
- How do I handle mod errors? → See [README_DEVELOPMENT.md](../../README_DEVELOPMENT.md)
