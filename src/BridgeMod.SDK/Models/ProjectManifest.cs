// =============================================================================
// ProjectManifest.cs — BridgeMod.SDK v0.6.0 — Interface Alpha
// =============================================================================
// Copyright (c) 2024 DreamCraft: Legacies Project
// License: MIT (see LICENSE in repository root)
//
// Description:
//   Manifest data object that reports BridgeMod's governance status and
//   logic fingerprint. Designed for consumption by Thalamus visual IDE
//   and Kanon governance system.
//
// =============================================================================

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BridgeMod.Bridge.Models
{
    /// <summary>
    /// Governance status of a BridgeMod project.
    /// </summary>
    public enum GovernanceStatus
    {
        /// <summary>No governance certificate present. Mods are validated locally only.</summary>
        Ungoverned = 0,

        /// <summary>Audit has been requested but not yet completed. Provisional governance state.</summary>
        PendingAudit = 1,

        /// <summary>Kanon or equivalent governance system has verified and signed the project.</summary>
        GovernedByKanon = 2
    }

    /// <summary>
    /// Project manifest that describes a BridgeMod host's governance status and logic fingerprint.
    /// This is the "security dashboard" snapshot for Thalamus visual IDE.
    /// </summary>
    public class ProjectManifest
    {
        /// <summary>Unique identifier for this project (e.g., game title or studio ID).</summary>
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>Version string of the project (e.g., "0.6.0" or "1.0.0").</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>UTC timestamp of when this manifest was generated.</summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>Count of registered mod surfaces (BehaviorGraphs, Data, ProceduralInputs).</summary>
        public int ActiveSurfacesCount { get; set; }

        /// <summary>
        /// SHA256 hash (as lowercase hex string) of all registered surfaces, weight tables,
        /// and behavior graphs. This fingerprint is what Kanon will cryptographically sign
        /// for governance.
        /// </summary>
        public string LogicFingerprint { get; set; } = string.Empty;

        /// <summary>Current governance status of this project.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public GovernanceStatus GovernanceStatus { get; set; }

        /// <summary>
        /// Serialize this manifest to a JSON string suitable for the Thalamus dashboard.
        /// </summary>
        /// <returns>Indented JSON representation of this manifest.</returns>
        public string ToJson()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            };
            return JsonSerializer.Serialize(this, options);
        }
    }
}
