using Newtonsoft.Json;

namespace BridgeMod.Data
{
    /// <summary>
    /// Represents the manifest file of a mod package.
    /// This is the authoritative declaration of mod metadata and contents.
    /// </summary>
    public class ModManifest
    {
        [JsonProperty("name", Required = Required.Always)]
        public required string Name { get; set; }

        [JsonProperty("version", Required = Required.Always)]
        public required string Version { get; set; }

        [JsonProperty("author", Required = Required.Always)]
        public required string Author { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("modType", Required = Required.Always)]
        public required ModType ModType { get; set; }

        [JsonProperty("dependencies")]
        public string[]? Dependencies { get; set; }

        [JsonProperty("files", Required = Required.Always)]
        public required string[] Files { get; set; }

        [JsonProperty("compatibilityNotes")]
        public string? CompatibilityNotes { get; set; }

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
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ModType
    {
        [JsonProperty("data")]
        Data,

        [JsonProperty("behaviorGraph")]
        BehaviorGraph,

        [JsonProperty("procedural")]
        Procedural
    }

    /// <summary>
    /// Handles semantic versioning for mods.
    /// </summary>
    public class ModVersion
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Patch { get; set; }

        public ModVersion(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

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

        public override string ToString() => $"{Major}.{Minor}.{Patch}";

        /// <summary>
        /// Checks if this version is compatible with a requirement (>=).
        /// </summary>
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
