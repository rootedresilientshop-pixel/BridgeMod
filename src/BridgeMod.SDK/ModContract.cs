// =============================================================================
// ModContract.cs — BridgeMod.SDK Phase 3 Foundation
// =============================================================================
// Copyright (c) 2024 DreamCraft: Legacies Project
// License: MIT (see LICENSE in repository root)
//
// Description:
//   Canonical mod contract types. These structures define the stable,
//   engine-neutral shape of mod payloads as they pass through the BridgeMod
//   firewall. This namespace is the authoritative definition source for all
//   mod-related data structures.
//
//   Engines do not define these types. Engines implement adapters that consume
//   validated payloads that conform to this contract.
//
//   All types in this namespace are:
//   - Immutable or tightly constrained
//   - Engine-agnostic (no engine-specific types)
//   - Serialization-safe (JSON-compatible)
//   - Additive-only (forward-compatible)
//   - Deterministic (no randomness, no reflection)
//
// =============================================================================

using System;
using System.Collections.Generic;

namespace BridgeMod.Bridge.ModContract
{
    /// <summary>
    /// The canonical manifest structure for all BridgeMod mod packages.
    /// This is the authoritative definition; all engines consume mods that
    /// conform to this structure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every mod package (.zip) must contain a manifest.json file at the root
    /// that deserializes to this structure (or a later-versioned variant).
    /// </para>
    /// <para>
    /// This structure is immutable after construction. Properties are read-only
    /// or auto-properties without setters.
    /// </para>
    /// <para>
    /// New optional fields may be added in future versions, but existing fields
    /// are never renamed or removed.
    /// </para>
    /// </remarks>
    public sealed class ModManifest
    {
        /// <summary>
        /// The semantic version of the manifest structure itself (not the mod version).
        /// Example: "1.0" (Phase 2), "2.0" (Phase 3+)
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// The mod's human-readable name.
        /// Must not be null or whitespace.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The mod's semantic version (e.g., "1.0.0", "2.1.0-beta").
        /// Must not be null or whitespace.
        /// </summary>
        public string ModVersion { get; set; } = string.Empty;

        /// <summary>
        /// The author or team who created the mod.
        /// May be null or whitespace (optional).
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// A short description of what the mod does.
        /// May be null or whitespace (optional).
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// A list of mod surface identifiers that this mod targets.
        /// Example: ["WeaponBalance", "CharacterStats"]
        /// These must match surfaces declared by the host game.
        /// If empty, the mod targets no specific surfaces (data files only).
        /// </summary>
        public IList<string> TargetSurfaces { get; set; } = new List<string>();

        /// <summary>
        /// Deterministic constraints metadata. Describes what the mod guarantees about itself.
        /// </summary>
        public ModConstraints? Constraints { get; set; }
    }

    /// <summary>
    /// Declares deterministic constraints that a mod adheres to.
    /// Used for host validation and mod categorization.
    /// </summary>
    public sealed class ModConstraints
    {
        /// <summary>
        /// If true, this mod contains only data (JSON, tables, configs).
        /// If false, the mod may contain behavior graphs, procedural rules, or other executable declarations.
        /// </summary>
        public bool DataOnly { get; set; } = true;

        /// <summary>
        /// The maximum execution time (in milliseconds) this mod allows for any single operation.
        /// If null, no explicit limit is declared.
        /// </summary>
        public int? MaxExecutionMs { get; set; }

        /// <summary>
        /// If true, this mod declares that it is deterministic (same input → same output every time).
        /// Engines may use this to optimize caching or replay scenarios.
        /// </summary>
        public bool Deterministic { get; set; } = true;
    }

    /// <summary>
    /// The canonical payload container for mod data as it passes through validation.
    /// All mod payloads, after passing the BridgeMod firewall, are structured as a ModPayload.
    /// </summary>
    public sealed class ModPayload
    {
        /// <summary>
        /// The manifest associated with this payload.
        /// Must not be null.
        /// </summary>
        public ModManifest? Manifest { get; set; }

        /// <summary>
        /// The raw, sanitized mod data as a JSON string.
        /// This data has been validated and bounds-checked by the BridgeMod firewall.
        /// Engines must NOT re-validate this data.
        /// </summary>
        public string Data { get; set; } = string.Empty;

        /// <summary>
        /// Metadata about the validation that produced this payload.
        /// Records which guards were applied, which checks passed, etc.
        /// </summary>
        public ValidationMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// Metadata about the validation process that produced a ModPayload.
    /// Provides engines with insight into what validation was applied.
    /// </summary>
    public sealed class ValidationMetadata
    {
        /// <summary>
        /// The timestamp when this payload was validated (UTC ticks).
        /// </summary>
        public long ValidatedAtUtcTicks { get; set; }

        /// <summary>
        /// The version of the BridgeMod SDK that performed validation.
        /// Example: "0.3.0"
        /// </summary>
        public string? ValidatorVersion { get; set; }

        /// <summary>
        /// The number of boundary guards that were applied during validation.
        /// </summary>
        public int GuardsApplied { get; set; }

        /// <summary>
        /// The number of values that were clamped or corrected by guards.
        /// If > 0, the payload was modified from the original mod submission.
        /// </summary>
        public int ValuesAdjusted { get; set; }
    }

    /// <summary>
    /// Describes a deterministic constraint violation encountered during validation.
    /// Failures do not prevent mod loading in the SDK; the host decides response.
    /// </summary>
    public sealed class ValidationFailure
    {
        /// <summary>
        /// The path in the JSON payload where the failure occurred.
        /// Example: "character[0].health" or "weapons.sword.damage"
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// The specific error code (from ErrorCodes).
        /// Example: "PARSE_ERR_001", "STAT_EXCEED_001"
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of what went wrong.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The value that was rejected or adjusted.
        /// </summary>
        public string? Value { get; set; }
    }

    /// <summary>
    /// Phase 3+ feature: Describes deterministic execution constraints for behavior graphs and procedural systems.
    /// Reserved for future use; not used in Phase 2.
    /// </summary>
    public sealed class ExecutionConstraints
    {
        /// <summary>
        /// Maximum nodes in any single graph structure.
        /// Prevents pathological graphs that could consume unbounded memory.
        /// </summary>
        public int MaxNodesPerGraph { get; set; } = 1000;

        /// <summary>
        /// Maximum edges (transitions) in any single graph.
        /// </summary>
        public int MaxEdgesPerGraph { get; set; } = 5000;

        /// <summary>
        /// Maximum nesting depth for compound structures (graphs containing graphs).
        /// </summary>
        public int MaxNestingDepth { get; set; } = 10;

        /// <summary>
        /// Maximum execution steps (node evaluations) before forced timeout.
        /// Prevents infinite loops in behavior graph execution.
        /// </summary>
        public int MaxExecutionSteps { get; set; } = 10000;

        /// <summary>
        /// Maximum memory (in bytes) a single mod can use during execution.
        /// Prevents memory exhaustion attacks.
        /// </summary>
        public long MaxMemoryBytes { get; set; } = 104857600;  // 100 MB
    }
}
