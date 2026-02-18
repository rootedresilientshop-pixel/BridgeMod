// =============================================================================
// ModSurfaceRegistry.cs — BridgeMod.SDK Phase 2 — Developer Mod Surfaces
// =============================================================================
// Copyright (c) 2024 DreamCraft: Legacies Project
// License: MIT (see LICENSE in repository root)
//
// Description:
//   A host-level registry that accumulates ModSurfaceDeclaration instances
//   during the game's initialization phase. Once the game is running, the
//   registry should be considered sealed — no further registrations should
//   occur.
//
//   The registry exposes its contents as IReadOnlyList<ModSurfaceDeclaration>
//   for consumption by tooling (e.g. ModSurfaceSummaryGenerator) and by any
//   host-authored introspection code.
//
//   The registry has no integration with the execution pipeline, validation
//   logic, audit logger, or any runtime component. It is a pure collection
//   of governance metadata.
// =============================================================================

using System;
using System.Collections.Generic;

namespace BridgeMod.Bridge
{
    /// <summary>
    /// A host-level registry of <see cref="ModSurfaceDeclaration"/> instances
    /// that documents the full set of mod surfaces exposed by a game.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Initialization Contract:</strong> This registry must be populated
    /// during the host's initialization phase only, before gameplay begins.
    /// Registering surfaces after initialization is architecturally incorrect
    /// and may produce inconsistent capability matrix output. The registry
    /// provides no enforcement of this contract — it is the host's responsibility.
    /// </para>
    /// <para>
    /// The registry does not integrate with the runtime engine, extension pipeline,
    /// validation firewall, or audit logger. Surfaces cannot be removed once
    /// registered. Duplicate surface names (case-sensitive) are rejected.
    /// </para>
    /// <para>
    /// Mods cannot access or influence the registry. Only host code registers surfaces.
    /// </para>
    /// </remarks>
    public sealed class ModSurfaceRegistry
    {
        private readonly List<ModSurfaceDeclaration> _surfaces = new List<ModSurfaceDeclaration>();

        /// <summary>
        /// A read-only view of all registered mod surfaces, in registration order.
        /// </summary>
        public IReadOnlyList<ModSurfaceDeclaration> Surfaces => _surfaces.AsReadOnly();

        /// <summary>
        /// Registers a <see cref="ModSurfaceDeclaration"/> with this registry.
        /// </summary>
        /// <param name="declaration">
        /// The surface declaration to register. Must not be null.
        /// The declaration's <see cref="ModSurfaceDeclaration.Name"/> must be
        /// unique within this registry (case-sensitive).
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="declaration"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a surface with the same <see cref="ModSurfaceDeclaration.Name"/>
        /// has already been registered.
        /// </exception>
        public void Register(ModSurfaceDeclaration declaration)
        {
            if (declaration == null)
                throw new ArgumentNullException(nameof(declaration), "Surface declaration must not be null.");

            foreach (ModSurfaceDeclaration existing in _surfaces)
            {
                if (string.Equals(existing.Name, declaration.Name, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"A surface named '{declaration.Name}' is already registered. " +
                        "Surface names must be unique within a registry.");
            }

            _surfaces.Add(declaration);
        }
    }
}
