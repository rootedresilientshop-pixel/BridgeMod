# BridgeMod Examples

This directory contains working examples of how to use BridgeMod in your game.

## Structure

- **Phase1Example.cs** - Shows how to declare mod surfaces, load mods, and handle errors
- **sample-mods/** - Complete, ready-to-use example mods

## Quick Start

### 1. See How to Use BridgeMod

Open [Phase1Example.cs](Phase1Example.cs):
- Shows how to create a `ModSurfaceDeclaration`
- Shows how to load a mod with `ModLoader`
- Shows how to handle errors safely

```csharp
var surfaces = new ModSurfaceDeclaration("MyGame", "1.0.0");
surfaces.DeclareDataSurface("Balance", "Game balance", "data/balance.json");

var loader = new ModLoader(new ModValidator());
var mod = loader.LoadMod("my_mod.zip");
```

### 2. See What a Complete Mod Looks Like

Navigate to [sample-mods/basic-balance-mod/](sample-mods/basic-balance-mod/):
- `manifest.json` - Mod metadata (required)
- `data/balance.json` - The actual mod content
- `README.md` - Explanation of what it does

This is a real, valid BridgeMod v1 mod that your game can load.

## How to Use These Examples

### For Game Developers

1. **Read Phase1Example.cs** to understand the API
2. **Copy the pattern** into your game's initialization
3. **Replace "MyGame"** with your actual game name
4. **Declare your actual mod surfaces** (what modders can change)
5. **Test with sample-mods/basic-balance-mod/**

### For Modders

1. **Study sample-mods/basic-balance-mod/** structure
2. **Copy the manifest.json** as a template
3. **Create your own data files** matching the structure
4. **Package as ZIP** with the same layout
5. **Test with your favorite game** that uses BridgeMod

## The Examples Demonstrate

✅ **Valid Mod Structure** - How files and metadata should be organized

✅ **Realistic Data** - Actual game-like balance values (weapons, difficulty)

✅ **Clear Manifest** - All required fields, commented

✅ **Working Integration** - Code that actually compiles and runs

✅ **Error Handling** - What happens when things go wrong

## What You Can Change

In **basic-balance-mod/data/balance.json**:

```json
{
  "weapons": {
    "sword": {
      "damage": 25,           ← Change any of these
      "attackSpeed": 1.0,
      "cost": 100
    }
  }
}
```

Or add entirely new weapons:

```json
{
  "weapons": {
    "laser": {              ← Your custom weapon
      "damage": 50,
      "attackSpeed": 2.0,
      "cost": 500
    }
  }
}
```

The **only constraint** is that your game's code must be able to handle whatever structure you put in the data file.

## File Size

These examples are intentionally simple and small:
- ~50 lines total (manifest + data + docs)
- Easy to understand
- Fast to copy and modify
- Real enough to test with

## Questions?

- **"How do I make a behavior graph mod?"** → Phase 2 (coming later). For now, data mods only.
- **"Can I add more files?"** → Yes! List them in `manifest.json` `files` array.
- **"What if my game needs different structure?"** → Read [MOD_SCHEMA.md](../MOD_SCHEMA.md) to understand the validation rules.
- **"How do I test this with my game?"** → Follow [QUICKSTART.md](../QUICKSTART.md) for integration steps.

## Next

1. Build and run [Phase1Example.cs](Phase1Example.cs)
2. Try loading [sample-mods/basic-balance-mod/](sample-mods/basic-balance-mod/)
3. Modify the balance values and see your game change
4. Create your own mod following the same pattern

---

**These examples show what's possible. Your game's mod surfaces will define what's allowed.**
