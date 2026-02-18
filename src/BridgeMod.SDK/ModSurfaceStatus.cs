// =============================================================================
// ModSurfaceStatus.cs — BridgeMod.SDK Phase 2 — Developer Mod Surfaces
// =============================================================================
// Copyright (c) 2024 DreamCraft: Legacies Project
// License: MIT (see LICENSE in repository root)
//
// Description:
//   Expresses the host-controlled availability state of a declared mod surface.
//   The host (game developer) assigns a status to each surface they register.
//   Modders and tooling consumers read this status as a transparency signal.
//
//   Status values carry no runtime enforcement — they are governance metadata.
//   Enforcement is the responsibility of the host's integration layer.
// =============================================================================

namespace BridgeMod.Bridge
{
    /// <summary>
    /// Describes the current availability state of a <see cref="ModSurfaceDeclaration"/>
    /// as declared by the host game developer.
    /// </summary>
    /// <remarks>
    /// Status values are governance metadata only. They do not alter validation
    /// logic, execution pipelines, or any runtime behavior. The host retains
    /// full control over how status is acted upon in their integration.
    /// Mods cannot read or influence surface status.
    /// </remarks>
    public enum ModSurfaceStatus
    {
        /// <summary>
        /// The surface is fully open for modding within its declared constraints.
        /// </summary>
        Enabled,

        /// <summary>
        /// The surface is available but with reduced scope or additional
        /// restrictions beyond the baseline. Developers should accompany
        /// this status with a descriptive <see cref="ModSurfaceDeclaration.Description"/>.
        /// </summary>
        Limited,

        /// <summary>
        /// The surface exists but is not available for modding at this time.
        /// Its declaration is retained for transparency — modders can see
        /// that the surface exists and why it is closed.
        /// </summary>
        Disabled,

        /// <summary>
        /// The surface is on the developer roadmap but has not yet been
        /// implemented or opened for modding. Declared early to signal
        /// intent to the modding community.
        /// </summary>
        Planned
    }
}
