// =============================================================================
// ModSurfaceSummaryGenerator.cs — BridgeMod.SDK Phase 2 — Developer Mod Surfaces
// =============================================================================
// Copyright (c) 2024 DreamCraft: Legacies Project
// License: MIT (see LICENSE in repository root)
//
// Description:
//   Generates a human-readable capability matrix from a ModSurfaceRegistry.
//   The output groups surfaces by category (alphabetically by category name),
//   then orders surfaces within each group alphabetically by surface name.
//
//   This generator performs no I/O. It produces a pure string. The caller
//   decides how to consume it — display, log, write to file, serve via API.
//
//   No runtime engine integration. No console output. No side effects.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;

namespace BridgeMod.Bridge
{
    /// <summary>
    /// Generates a formatted, human-readable capability matrix string from a
    /// <see cref="ModSurfaceRegistry"/>, suitable for presentation to modders
    /// or for inclusion in generated documentation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The generator is stateless and performs no I/O operations. It accepts
    /// a registry, groups its surfaces by <see cref="ModSurfaceCategory"/>,
    /// orders each group alphabetically by <see cref="ModSurfaceDeclaration.Name"/>,
    /// and returns a formatted string.
    /// </para>
    /// <para>
    /// No file writing, no console output, no network access, and no integration
    /// with the runtime engine or validation pipeline occurs within this class.
    /// </para>
    /// </remarks>
    public static class ModSurfaceSummaryGenerator
    {
        /// <summary>
        /// Generates a capability matrix string from the provided registry.
        /// Surfaces are grouped by category (categories sorted alphabetically)
        /// and ordered alphabetically by name within each group.
        /// </summary>
        /// <param name="registry">
        /// The registry whose surfaces form the matrix. Must not be null.
        /// </param>
        /// <param name="gameTitle">
        /// An optional title for the game or host. Defaults to "Game" if null or whitespace.
        /// </param>
        /// <returns>
        /// A formatted multi-line string representing the capability matrix.
        /// Returns a notice string if the registry contains no surfaces.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="registry"/> is null.
        /// </exception>
        public static string Generate(ModSurfaceRegistry registry, string? gameTitle = null)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry), "Registry must not be null.");

            string title = string.IsNullOrWhiteSpace(gameTitle) ? "Game" : gameTitle;

            if (registry.Surfaces.Count == 0)
                return $"=== {title} — Mod Capability Matrix ==={Environment.NewLine}No surfaces declared.";

            // Group surfaces by category
            Dictionary<ModSurfaceCategory, List<ModSurfaceDeclaration>> groups =
                new Dictionary<ModSurfaceCategory, List<ModSurfaceDeclaration>>();

            foreach (ModSurfaceDeclaration surface in registry.Surfaces)
            {
                if (!groups.ContainsKey(surface.Category))
                    groups[surface.Category] = new List<ModSurfaceDeclaration>();

                groups[surface.Category].Add(surface);
            }

            // Sort each group's surfaces alphabetically by name
            List<ModSurfaceCategory> sortedCategories = new List<ModSurfaceCategory>(groups.Keys);
            sortedCategories.Sort((a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal));

            foreach (ModSurfaceCategory category in sortedCategories)
                groups[category].Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

            // Build output
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== {title} — Mod Capability Matrix ===");
            sb.AppendLine();

            foreach (ModSurfaceCategory category in sortedCategories)
            {
                sb.AppendLine($"[{category}]");

                foreach (ModSurfaceDeclaration surface in groups[category])
                {
                    sb.AppendLine($"  {surface.Name} [{surface.Status}]");
                    sb.AppendLine($"    {surface.Description}");
                }

                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }
    }
}
