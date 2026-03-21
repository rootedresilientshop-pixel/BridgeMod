// =============================================================================
// IGovernanceProvider.cs — BridgeMod.SDK Thalamus Integration Interface
// =============================================================================
// Copyright (c) 2024 DreamCraft: Legacies Project
// License: MIT (see LICENSE in repository root)
//
// Description:
//   Governance provider interface for Project Thalamus visual IDE integration.
//   Allows BridgeMod to remain "Kanon-ready" without requiring Kanon source in this repository.
//   Implementations (Kanon) handle manifest verification and audit requests externally.
//
// =============================================================================

using System;
using System.Threading.Tasks;

namespace BridgeMod.Bridge.Governance
{
    /// <summary>
    /// Contract for external governance providers (e.g., Kanon).
    /// Allows BridgeMod to delegate governance decisions to external systems
    /// without hard dependencies on those systems.
    /// </summary>
    public interface IGovernanceProvider
    {
        /// <summary>
        /// Verifies a mod manifest JSON against governance rules.
        /// </summary>
        /// <param name="manifestJson">The serialized mod manifest JSON to verify.</param>
        /// <returns>
        /// <c>true</c> if the manifest passes governance checks; <c>false</c> otherwise.
        /// Implementations may throw <see cref="ArgumentException"/> for malformed JSON.
        /// </returns>
        bool VerifyManifest(string manifestJson);

        /// <summary>
        /// Asynchronously requests an audit report for a mod by its ID.
        /// Used to fetch detailed audit information from external governance systems.
        /// </summary>
        /// <param name="modId">The unique mod identifier (typically from manifest).</param>
        /// <returns>
        /// A governance audit report (implementation-defined structure, typically JSON).
        /// Returns <c>null</c> if no audit record exists for the given modId.
        /// </returns>
        Task<string?> RequestAuditAsync(string modId);

        /// <summary>
        /// Checks whether a mod is allowed to declare a specific surface category.
        /// Governance rules may restrict certain categories to specific mod authors or signing keys.
        /// </summary>
        /// <param name="modId">The unique mod identifier.</param>
        /// <param name="surfaceCategory">The surface category being requested (e.g., "Data", "BehaviorGraphs", "ProceduralInputs").</param>
        /// <returns>
        /// <c>true</c> if the mod is authorized to use this surface; <c>false</c> otherwise.
        /// </returns>
        bool IsAuthorizedForSurface(string modId, string surfaceCategory);
    }
}
