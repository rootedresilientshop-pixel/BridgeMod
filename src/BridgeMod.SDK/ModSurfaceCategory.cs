// =============================================================================
// ModSurfaceCategory.cs — BridgeMod.SDK Phase 2 — Developer Mod Surfaces
// =============================================================================
// Copyright (c) 2024 DreamCraft: Legacies Project
// License: MIT (see LICENSE in repository root)
//
// Description:
//   Declares the high-level categories under which a game developer may
//   classify a declared mod surface. Categories drive the grouping logic
//   in capability matrix generation and provide semantic clarity to both
//   developers and tooling consumers.
//
//   This enum carries no runtime behavior. It is metadata-only.
// =============================================================================

namespace BridgeMod.Bridge
{
    /// <summary>
    /// Classifies a <see cref="ModSurfaceDeclaration"/> into a high-level domain.
    /// Used for grouping in capability matrix output and for developer-facing
    /// documentation of which areas of the game are open to modding.
    /// </summary>
    /// <remarks>
    /// Categories are purely declarative — they carry no runtime semantics and
    /// do not alter validation logic, execution pipelines, or extension hooks.
    /// </remarks>
    public enum ModSurfaceCategory
    {
        /// <summary>
        /// Covers purely data-driven surfaces such as JSON balance sheets,
        /// stat tables, item definitions, and configuration files.
        /// No executable content is permitted in this category.
        /// </summary>
        Data,

        /// <summary>
        /// Covers state machines and event-condition-action (ECA) rule graphs
        /// expressed as declarative node graphs. No scripting is involved.
        /// </summary>
        BehaviorGraphs,

        /// <summary>
        /// Covers procedural generation control surfaces such as seeds,
        /// weightings, and generation parameters. All values are numeric
        /// or symbolic — no executable logic is introduced.
        /// </summary>
        ProceduralInputs
    }
}
