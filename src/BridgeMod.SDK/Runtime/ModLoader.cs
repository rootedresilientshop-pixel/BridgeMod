using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using BridgeMod.Data;

namespace BridgeMod.Runtime
{
    /// <summary>
    /// Loads validated mod packages safely.
    /// Implements failure isolation and automatic error recovery.
    /// </summary>
    public class ModLoader
    {
        private readonly ModValidator _validator;
        private readonly Dictionary<string, LoadedMod> _loadedMods;
        private readonly ExecutionGuards _guards;

        /// <summary>
        /// Initializes a new ModLoader with the specified mod validator.
        /// </summary>
        /// <param name="validator">The ModValidator instance to use for validating mod packages.</param>
        public ModLoader(ModValidator validator)
        {
            _validator = validator;
            _loadedMods = new Dictionary<string, LoadedMod>();
            _guards = new ExecutionGuards();
        }

        /// <summary>
        /// Loads a mod package, validates it, and makes it available for execution.
        /// Returns null if validation fails or mod is disabled due to errors.
        /// </summary>
        public LoadedMod? LoadMod(string modPackagePath)
        {
            var validation = _validator.ValidateModPackage(modPackagePath);

            if (!validation.IsValid)
            {
                _guards.DisableMod(modPackagePath, string.Join("; ", validation.Errors));
                return null;
            }

            if (validation.Manifest == null)
                return null;

            // Check for existing mod with same name
            if (_loadedMods.ContainsKey(validation.Manifest.Name))
            {
                return null; // Mod already loaded
            }

            try
            {
                var mod = new LoadedMod
                {
                    Name = validation.Manifest.Name,
                    Version = validation.Manifest.Version,
                    Author = validation.Manifest.Author,
                    Description = validation.Manifest.Description,
                    ModType = validation.Manifest.ModType,
                    FilePath = modPackagePath,
                    LoadTime = DateTime.UtcNow,
                    IsEnabled = true,
                    Manifest = validation.Manifest
                };

                // Load mod contents into memory
                if (!LoadModContents(mod))
                {
                    _guards.DisableMod(modPackagePath, "Failed to load mod contents");
                    return null;
                }

                _loadedMods[mod.Name] = mod;
                return mod;
            }
            catch (Exception ex)
            {
                _guards.DisableMod(modPackagePath, $"Unexpected error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets a loaded mod by name.
        /// </summary>
        public LoadedMod? GetMod(string modName)
        {
            _loadedMods.TryGetValue(modName, out var mod);
            return mod != null && mod.IsEnabled ? mod : null;
        }

        /// <summary>
        /// Gets all currently loaded and enabled mods.
        /// </summary>
        public IEnumerable<LoadedMod> GetAllLoadedMods() =>
            _loadedMods.Values.Where(m => m.IsEnabled);

        /// <summary>
        /// Disables a mod (prevents execution but keeps in registry).
        /// </summary>
        public void DisableMod(string modName, string reason)
        {
            if (_loadedMods.TryGetValue(modName, out var mod))
            {
                mod.IsEnabled = false;
                mod.DisableReason = reason;
            }
        }

        /// <summary>
        /// Unloads a mod from memory.
        /// </summary>
        public bool UnloadMod(string modName)
        {
            if (_loadedMods.Remove(modName))
            {
                return true;
            }
            return false;
        }

        private bool LoadModContents(LoadedMod mod)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(mod.FilePath))
                {
                    foreach (var file in mod.Manifest.Files)
                    {
                        var entry = archive.GetEntry(file);
                        if (entry == null) continue;

                        using (var stream = entry.Open())
                        using (var reader = new StreamReader(stream))
                        {
                            var content = reader.ReadToEnd();
                            mod.Contents[file] = content;
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                mod.LastError = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Handles mod execution errors by disabling the mod automatically.
        /// </summary>
        public void HandleModError(string modName, Exception ex)
        {
            if (_loadedMods.TryGetValue(modName, out var mod))
            {
                mod.IsEnabled = false;
                mod.LastError = ex.Message;
                mod.ErrorCount++;
                _guards.DisableMod(mod.FilePath, ex.Message);
            }
        }
    }

    /// <summary>
    /// Represents a loaded mod in memory with its metadata and contents.
    /// </summary>
    public class LoadedMod
    {
        /// <summary>
        /// The unique name of this mod.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// The version of this mod.
        /// </summary>
        public required string Version { get; set; }

        /// <summary>
        /// The author or creator of this mod.
        /// </summary>
        public required string Author { get; set; }

        /// <summary>
        /// A human-readable description of what this mod does.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// The type of mod (Data, BehaviorGraph, or Procedural).
        /// </summary>
        public required ModType ModType { get; set; }

        /// <summary>
        /// The file path to the mod package (.zip file).
        /// </summary>
        public required string FilePath { get; set; }

        /// <summary>
        /// The UTC timestamp when this mod was loaded.
        /// </summary>
        public DateTime LoadTime { get; set; }

        /// <summary>
        /// True if this mod is currently enabled and can be executed.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// If disabled, the reason why this mod was disabled.
        /// </summary>
        public string? DisableReason { get; set; }

        /// <summary>
        /// The last error encountered when loading or executing this mod.
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// The number of errors encountered by this mod during execution.
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// The parsed manifest metadata for this mod.
        /// </summary>
        public required ModManifest Manifest { get; set; }

        /// <summary>
        /// The mod's file contents loaded into memory, keyed by file path.
        /// </summary>
        public Dictionary<string, string> Contents { get; } = new();

        /// <summary>
        /// Gets a specific file from the mod's contents.
        /// </summary>
        public string? GetFile(string filePath)
        {
            Contents.TryGetValue(filePath, out var content);
            return content;
        }
    }
}
