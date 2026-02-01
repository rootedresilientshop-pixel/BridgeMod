using BridgeMod.Data;

namespace BridgeMod.PublicAPI
{
    /// <summary>
    /// Public API for game developers to declare mod surfaces.
    /// This is how developers explicitly declare what modders can modify.
    /// </summary>
    public class ModSurfaceDeclaration
    {
        public string GameTitle { get; set; }
        public string GameVersion { get; set; }
        public List<ModSurface> Surfaces { get; set; } = new();

        public ModSurfaceDeclaration(string gameTitle, string gameVersion)
        {
            GameTitle = gameTitle;
            GameVersion = gameVersion;
        }

        /// <summary>
        /// Declares a data mod surface.
        /// </summary>
        public ModSurface DeclareDataSurface(
            string name,
            string description,
            string filePath,
            SurfaceStatus status = SurfaceStatus.Enabled)
        {
            var surface = new ModSurface
            {
                Name = name,
                Description = description,
                ModType = ModType.Data,
                FilePath = filePath,
                Status = status
            };

            Surfaces.Add(surface);
            return surface;
        }

        /// <summary>
        /// Declares a behavior graph mod surface.
        /// </summary>
        public ModSurface DeclareBehaviorGraphSurface(
            string name,
            string description,
            string filePath,
            int maxNodeCount = 1000,
            int maxGraphDepth = 50,
            SurfaceStatus status = SurfaceStatus.Enabled)
        {
            var surface = new ModSurface
            {
                Name = name,
                Description = description,
                ModType = ModType.BehaviorGraph,
                FilePath = filePath,
                Status = status,
                Constraints = new GraphConstraints
                {
                    MaxNodeCount = maxNodeCount,
                    MaxGraphDepth = maxGraphDepth
                }
            };

            Surfaces.Add(surface);
            return surface;
        }

        /// <summary>
        /// Declares a procedural mod surface.
        /// </summary>
        public ModSurface DeclareProcedurtalSurface(
            string name,
            string description,
            string filePath,
            Dictionary<string, (double Min, double Max)>? parameterBounds = null,
            SurfaceStatus status = SurfaceStatus.Enabled)
        {
            var surface = new ModSurface
            {
                Name = name,
                Description = description,
                ModType = ModType.Procedural,
                FilePath = filePath,
                Status = status,
                Constraints = new ProceduralConstraints
                {
                    ParameterBounds = parameterBounds ?? new()
                }
            };

            Surfaces.Add(surface);
            return surface;
        }

        /// <summary>
        /// Generates a human-readable summary of all declared mod surfaces.
        /// </summary>
        public string GenerateSurfaceSummary()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# Mod Surfaces for {GameTitle} v{GameVersion}");
            sb.AppendLine();

            if (Surfaces.Count == 0)
            {
                sb.AppendLine("No mod surfaces declared.");
                return sb.ToString();
            }

            var byType = Surfaces.GroupBy(s => s.ModType);

            foreach (var group in byType)
            {
                sb.AppendLine($"## {group.Key} Mods");
                sb.AppendLine();

                foreach (var surface in group)
                {
                    sb.AppendLine($"### {surface.Name}");
                    sb.AppendLine($"**Status:** {surface.Status}");
                    sb.AppendLine($"**Description:** {surface.Description}");
                    sb.AppendLine($"**File Path:** `{surface.FilePath}`");

                    if (surface.Constraints is GraphConstraints gc)
                    {
                        sb.AppendLine($"**Max Nodes:** {gc.MaxNodeCount}");
                        sb.AppendLine($"**Max Depth:** {gc.MaxGraphDepth}");
                    }

                    if (surface.Constraints is ProceduralConstraints pc && pc.ParameterBounds.Count > 0)
                    {
                        sb.AppendLine("**Parameter Bounds:**");
                        foreach (var bound in pc.ParameterBounds)
                        {
                            sb.AppendLine($"  - `{bound.Key}`: [{bound.Value.Min}, {bound.Value.Max}]");
                        }
                    }

                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports the surface declaration as JSON for documentation.
        /// </summary>
        public string ExportAsJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(
                new
                {
                    GameTitle,
                    GameVersion,
                    Surfaces = Surfaces.Select(s => new
                    {
                        s.Name,
                        s.Description,
                        s.ModType,
                        s.FilePath,
                        s.Status
                    })
                },
                Newtonsoft.Json.Formatting.Indented);
        }
    }

    /// <summary>
    /// Represents a single mod surface that a developer declares.
    /// </summary>
    public class ModSurface
    {
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required ModType ModType { get; set; }
        public required string FilePath { get; set; }
        public SurfaceStatus Status { get; set; } = SurfaceStatus.Enabled;
        public SurfaceConstraints? Constraints { get; set; }
        public DateTime DeclaredAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Marks this surface as limited (requires mod developer approval).
        /// </summary>
        public void MarkAsLimited(string reason)
        {
            Status = SurfaceStatus.Limited;
        }

        /// <summary>
        /// Disables this mod surface entirely.
        /// </summary>
        public void Disable(string reason)
        {
            Status = SurfaceStatus.Disabled;
        }

        /// <summary>
        /// Marks this surface as planned for a future version.
        /// </summary>
        public void MarkAsPlanned(string targetVersion)
        {
            Status = SurfaceStatus.Planned;
        }
    }

    /// <summary>
    /// Status of a mod surface.
    /// </summary>
    public enum SurfaceStatus
    {
        Enabled,   // Mods can use this surface freely
        Limited,   // Limited access, may require approval
        Disabled,  // Mod surface is disabled
        Planned    // Will be available in future version
    }

    /// <summary>
    /// Base class for surface constraints.
    /// </summary>
    public abstract class SurfaceConstraints
    {
    }

    /// <summary>
    /// Constraints for behavior graph surfaces.
    /// </summary>
    public class GraphConstraints : SurfaceConstraints
    {
        public int MaxNodeCount { get; set; } = 1000;
        public int MaxGraphDepth { get; set; } = 50;
    }

    /// <summary>
    /// Constraints for procedural surfaces.
    /// </summary>
    public class ProceduralConstraints : SurfaceConstraints
    {
        public Dictionary<string, (double Min, double Max)> ParameterBounds { get; set; } = new();
    }
}
