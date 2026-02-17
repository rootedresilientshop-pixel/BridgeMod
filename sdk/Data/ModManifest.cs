using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BridgeMod.Data
{
    /// <summary>
    /// Represents the manifest file of a mod package.
    /// This is the authoritative declaration of mod metadata and contents.
    /// </summary>
    public class ModManifest
    {
        /// <summary>
        /// The unique identifier for this mod.
        /// </summary>
        [JsonProperty("name", Required = Required.Always)]
        public required string Name { get; set; }

        /// <summary>
        /// Semantic version of this mod (major.minor.patch).
        /// </summary>
        [JsonProperty("version", Required = Required.Always)]
        public required string Version { get; set; }

        /// <summary>
        /// The author or team that created this mod.
        /// </summary>
        [JsonProperty("author", Required = Required.Always)]
        public required string Author { get; set; }

        /// <summary>
        /// Human-readable description of what this mod does.
        /// </summary>
        [JsonProperty("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Type of mod: Data, BehaviorGraph, or Procedural.
        /// </summary>
        [JsonProperty("modType", Required = Required.Always)]
        public required ModType ModType { get; set; }

        /// <summary>
        /// List of mods this mod depends on, formatted as "modname@1.0.0".
        /// </summary>
        [JsonProperty("dependencies")]
        public string[]? Dependencies { get; set; }

        /// <summary>
        /// Relative paths to all files contained in this mod package.
        /// </summary>
        [JsonProperty("files", Required = Required.Always)]
        public required string[] Files { get; set; }

        /// <summary>
        /// Notes about compatibility with specific game versions or other mods.
        /// </summary>
        [JsonProperty("compatibilityNotes")]
        public string? CompatibilityNotes { get; set; }

        /// <summary>
        /// Searchable tags for categorizing this mod (e.g., "balance", "visual", "audio").
        /// </summary>
        [JsonProperty("tags")]
        public string[]? Tags { get; set; }

        /// <summary>
        /// Validates manifest integrity without loading mod contents.
        /// </summary>
        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Name))
                errors.Add("Manifest 'name' is required and cannot be empty");

            if (string.IsNullOrWhiteSpace(Version))
                errors.Add("Manifest 'version' is required and cannot be empty");

            if (!ModVersion.TryParse(Version, out _))
                errors.Add($"Version '{Version}' does not follow semantic versioning (major.minor.patch)");

            if (string.IsNullOrWhiteSpace(Author))
                errors.Add("Manifest 'author' is required and cannot be empty");

            if (Files == null || Files.Length == 0)
                errors.Add("Manifest 'files' array must contain at least one file");

            if (Dependencies != null)
            {
                foreach (var dep in Dependencies)
                {
                    if (!ModVersion.TryParseModDependency(dep, out _, out _))
                        errors.Add($"Dependency '{dep}' is malformed (expected format: modname@1.0.0)");
                }
            }

            return errors.Count == 0;
        }
    }

    /// <summary>
    /// Enum for supported mod types in v1.
    /// </summary>
    /// <summary>
    /// JSON-compatible string enum for mod type.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ModType
    {
        /// <summary>
        /// A pure data mod (JSON configuration files, balance tweaks, etc.).
        /// </summary>
        [JsonProperty("data")]
        Data,

        /// <summary>
        /// A behavior graph mod (state machines, AI decision trees, etc.).
        /// </summary>
        [JsonProperty("behaviorGraph")]
        BehaviorGraph,

        /// <summary>
        /// A procedural mod (world generation parameters, seeds, etc.).
        /// </summary>
        [JsonProperty("procedural")]
        Procedural
    }

    /// <summary>
    /// Handles semantic versioning for mods.
    /// </summary>
    public class ModVersion
    {
        /// <summary>
        /// Major version number (incremented for breaking changes).
        /// </summary>
        public int Major { get; set; }

        /// <summary>
        /// Minor version number (incremented for new features).
        /// </summary>
        public int Minor { get; set; }

        /// <summary>
        /// Patch version number (incremented for bugfixes).
        /// </summary>
        public int Patch { get; set; }

        /// <summary>
        /// Creates a new semantic version.
        /// </summary>
        /// <param name="major">Major version number.</param>
        /// <param name="minor">Minor version number.</param>
        /// <param name="patch">Patch version number.</param>
        public ModVersion(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        /// <summary>
        /// Attempts to parse a semantic version string (e.g., "1.2.3").
        /// </summary>
        /// <param name="version">The version string to parse.</param>
        /// <param name="result">The parsed ModVersion, or null if parsing fails.</param>
        /// <returns>True if parsing succeeded, false otherwise.</returns>
        public static bool TryParse(string version, out ModVersion? result)
        {
            result = null;
            var parts = version.Split('.');

            if (parts.Length != 3)
                return false;

            if (int.TryParse(parts[0], out var major) &&
                int.TryParse(parts[1], out var minor) &&
                int.TryParse(parts[2], out var patch))
            {
                result = new ModVersion(major, minor, patch);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parses mod dependency format: "modname@1.0.0"
        /// </summary>
        public static bool TryParseModDependency(string dependency, out string? modName, out ModVersion? version)
        {
            modName = null;
            version = null;

            var parts = dependency.Split('@');
            if (parts.Length != 2)
                return false;

            modName = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(modName))
                return false;

            return TryParse(parts[1], out version);
        }

        /// <summary>
        /// Returns the version as a semantic version string (major.minor.patch).
        /// </summary>
        public override string ToString() => $"{Major}.{Minor}.{Patch}";

        /// <summary>
        /// Checks if this version is compatible with a requirement (this >= required).
        /// </summary>
        /// <param name="required">The minimum required version.</param>
        /// <returns>True if this version is greater than or equal to the required version.</returns>
        public bool IsCompatibleWith(ModVersion required)
        {
            if (Major > required.Major) return true;
            if (Major < required.Major) return false;

            if (Minor > required.Minor) return true;
            if (Minor < required.Minor) return false;

            return Patch >= required.Patch;
        }
    }
}
