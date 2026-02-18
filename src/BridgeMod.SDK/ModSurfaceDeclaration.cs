// =============================================================================
// ModSurfaceDeclaration.cs — BridgeMod.SDK Phase 2 — Developer Mod Surfaces
// =============================================================================
// Copyright (c) 2024 DreamCraft: Legacies Project
// License: MIT (see LICENSE in repository root)
//
// Description:
//   A single, immutable record of a mod surface exposed by the host game.
//   Declarations are authored by the game developer and registered with a
//   ModSurfaceRegistry during the host's initialization phase.
//
//   A surface declaration states:
//     - What the surface is called (Name)
//     - What kind of data it accepts (Category)
//     - Whether it is currently available (Status)
//     - A human-readable explanation (Description)
//
//   Declarations carry no runtime hooks, no executable callbacks, and no
//   references to the engine pipeline. They are pure structural metadata.
//
//   Mods cannot create or modify surface declarations. Only the host can.
// =============================================================================

using System;

namespace BridgeMod.Bridge
{
    /// <summary>
    /// An immutable declaration of a single mod surface exposed by the host game.
    /// Surface declarations are authored by game developers and registered at
    /// initialization time to communicate modding boundaries to tooling and modders.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is sealed and immutable. All properties are assigned at
    /// construction and cannot be altered thereafter. There are no setters,
    /// no internal mutation methods, and no runtime callbacks.
    /// </para>
    /// <para>
    /// A surface declaration is governance metadata. It does not alter validation
    /// logic, extension pipelines, or deterministic execution guarantees. Mods
    /// cannot read, write, or influence surface declarations at runtime.
    /// </para>
    /// </remarks>
    public sealed class ModSurfaceDeclaration
    {
        /// <summary>
        /// The unique identifier name for this mod surface within a game's registry.
        /// Must be non-null and non-empty. Used as the primary key for grouping
        /// and alphabetical ordering in capability matrix output.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The high-level domain category of this surface.
        /// Drives grouping in the capability matrix generator.
        /// </summary>
        public ModSurfaceCategory Category { get; }

        /// <summary>
        /// The host-declared availability status of this surface.
        /// Communicates to modders and tooling whether this surface is
        /// currently open, restricted, closed, or forthcoming.
        /// </summary>
        public ModSurfaceStatus Status { get; }

        /// <summary>
        /// A human-readable description of what this surface exposes,
        /// any constraints that apply, and guidance for modders.
        /// Must be non-null and non-empty.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Initializes a new, immutable <see cref="ModSurfaceDeclaration"/>.
        /// </summary>
        /// <param name="name">
        /// The unique name of this surface. Must not be null or whitespace.
        /// </param>
        /// <param name="category">
        /// The domain category of this surface.
        /// </param>
        /// <param name="status">
        /// The current availability status of this surface.
        /// </param>
        /// <param name="description">
        /// A human-readable description. Must not be null or whitespace.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="name"/> or <paramref name="description"/>
        /// is null or whitespace.
        /// </exception>
        public ModSurfaceDeclaration(
            string name,
            ModSurfaceCategory category,
            ModSurfaceStatus status,
            string description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Surface name must not be null or whitespace.", nameof(name));

            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Surface description must not be null or whitespace.", nameof(description));

            Name = name;
            Category = category;
            Status = status;
            Description = description;
        }
    }
}
