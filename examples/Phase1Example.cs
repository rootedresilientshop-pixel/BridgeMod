// Example: Phase 1 - Safe Mod Loading and Execution
// This demonstrates how to use the BridgeMod SDK to load and validate mods

using BridgeMod.Data;
using BridgeMod.Runtime;
using BridgeMod.PublicAPI;

// Step 1: Create a mod validator and loader
var validator = new ModValidator();
var loader = new ModLoader(validator);

// Step 2: Define mod surfaces for your game
var gameModSurfaces = new ModSurfaceDeclaration("ExampleGame", "1.0.0");

// Declare data mod surface
gameModSurfaces.DeclareDataSurface(
    name: "Game Balance",
    description: "Weapon and enemy damage, health values",
    filePath: "data/balance.json",
    status: SurfaceStatus.Enabled);

// Declare behavior graph surface
gameModSurfaces.DeclareBehaviorGraphSurface(
    name: "Enemy AI",
    description: "Enemy state machines and behavior trees",
    filePath: "graphs/enemy_ai.graph.json",
    maxNodeCount: 500,
    maxGraphDepth: 20,
    status: SurfaceStatus.Enabled);

// Declare procedural surface
gameModSurfaces.DeclareProcedurtalSurface(
    name: "Procedural Generation",
    description: "World generation with seeds and parameters",
    filePath: "procedural/worldgen.json",
    parameterBounds: new Dictionary<string, (double, double)>
    {
        { "seed", (1, 1000000) },
        { "scale", (0.1, 10.0) },
        { "density", (0.0, 1.0) }
    },
    status: SurfaceStatus.Enabled);

// Step 3: Generate documentation for modders
var modSurfaceDoc = gameModSurfaces.GenerateSurfaceSummary();
Console.WriteLine("=== Available Mod Surfaces ===");
Console.WriteLine(modSurfaceDoc);

// Step 4: Load mods from a directory
Console.WriteLine("\n=== Loading Mods ===");
var modsDirectory = "Assets/Mods"; // In a real game, this would be a real directory
Console.WriteLine($"Looking for mods in: {modsDirectory}");

// For demonstration, show what would happen
// In real usage:
// foreach (var modFile in Directory.GetFiles(modsDirectory, "*.zip"))
// {
//     var mod = loader.LoadMod(modFile);
//     if (mod?.IsEnabled == true)
//     {
//         Console.WriteLine($"✓ Loaded: {mod.Name} v{mod.Version} by {mod.Author}");
//     }
//     else
//     {
//         Console.WriteLine($"✗ Failed to load: {modFile}");
//     }
// }

// Step 5: Execute mods with safety guards
Console.WriteLine("\n=== Mod Execution with Safety ===");
var guards = new ExecutionGuards();

// Create execution context for a mod
var context = guards.CreateExecutionContext("ExampleMod", timeoutMs: 5000);

try
{
    // Simulate mod execution
    Console.WriteLine($"Executing {context.ModName}...");

    // Validate file access
    var sandboxPath = Path.Combine(Path.GetTempPath(), "bridgemod_mods");
    var requestedFile = Path.Combine(sandboxPath, "data.json");

    if (guards.ValidateFilePath(requestedFile, sandboxPath))
    {
        Console.WriteLine("✓ File access validated");
    }

    // Validate procedural parameters
    if (guards.ValidateParameterBounds(0.5, 0.0, 1.0))
    {
        Console.WriteLine("✓ Procedural parameters within bounds");
    }

    // Simulate successful execution
    System.Threading.Thread.Sleep(100);
    context.Complete();

    Console.WriteLine($"✓ Execution completed in {context.ElapsedTime.TotalMilliseconds:F0}ms");
}
catch (Exception ex)
{
    context.RecordError(ex);
    guards.DisableMod("example_mod.zip", ex.Message);
    Console.WriteLine($"✗ Execution failed: {ex.Message}");
}

// Step 6: Report on mod status
Console.WriteLine("\n=== Mod Status ===");
var loadedMods = loader.GetAllLoadedMods();
Console.WriteLine($"Total loaded mods: {loadedMods.Count()}");

var disabledMods = guards.GetDisabledMods();
if (disabledMods.Any())
{
    Console.WriteLine($"\nDisabled mods ({disabledMods.Count()}):");
    foreach (var disabled in disabledMods)
    {
        Console.WriteLine($"  - {disabled.FilePath}");
        Console.WriteLine($"    Reason: {disabled.Reason}");
    }
}

// Step 7: Export surface declaration as JSON
Console.WriteLine("\n=== Surface Declaration Export ===");
var surfaceJson = gameModSurfaces.ExportAsJson();
Console.WriteLine(surfaceJson);

Console.WriteLine("\n=== Phase 1 Example Complete ===");
Console.WriteLine("The BridgeMod SDK is ready for:");
Console.WriteLine("  ✓ Safe mod loading with local validation");
Console.WriteLine("  ✓ Schema enforcement (data, behavior graphs, procedural)");
Console.WriteLine("  ✓ Execution guards and failure isolation");
Console.WriteLine("  ✓ Developer-defined mod surfaces");
Console.WriteLine("  ✓ Deterministic, sandbox execution");
Console.WriteLine("\nNext: Phase 2 focuses on enhanced developer surfaces and mod discovery.");
