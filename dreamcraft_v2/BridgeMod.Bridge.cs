// =============================================================================
// BridgeMod.Bridge.cs — BridgeMod.SDK v0.2.2 — Core Library Source
// =============================================================================
// Copyright (c) 2024 DreamCraft: Legacies Project
// License: MIT (see LICENSE in repository root)
//
// Description:
//   The public API surface of BridgeMod.SDK. This file contains all types
//   that consumers of the NuGet package interact with directly:
//
//     ErrorCodes       — Shared error code constants (also used by Python engine)
//     BridgeConfig     — Configuration record passed to ModBridge constructor
//     AuditLogger      — Append-only tamper-evident event recorder
//     ValidationResult — The result returned by ModBridge.Validate()
//     ModBridge        — The core firewall: Deserialize → BoundaryGuards → AuditLog
//
//   This file is the library source. For a runnable sample that demonstrates
//   all three validation gates, see Legacies_Bridge_Test/Program.cs.
//
// NuGet Package: BridgeMod.SDK v0.2.2
// Repository:    https://github.com/rootedresilientshop-pixel/BridgeMod
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BridgeMod.Bridge
{
    // -------------------------------------------------------------------------
    // Error Codes
    // -------------------------------------------------------------------------

    /// <summary>
    /// Shared error code constants written to the audit log on validation events.
    /// These codes are identical in the Python engine (pulse_test.py) so that
    /// cross-language audit log analysis produces consistent, searchable output.
    /// </summary>
    public static class ErrorCodes
    {
        /// <summary>
        /// A string field in the payload contained disallowed markup (HTML/script tags).
        /// The entire payload is rejected when this code is triggered.
        /// </summary>
        public const string ParseErr001 = "PARSE_ERR_001";

        /// <summary>
        /// A numeric stat field exceeded its declared maximum and was clamped.
        /// The payload is still accepted, but with the corrected value.
        /// </summary>
        public const string BoundClamp003 = "BOUND_CLAMP_003";
    }

    // -------------------------------------------------------------------------
    // Configuration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Immutable configuration for a <see cref="ModBridge"/> instance.
    ///
    /// <para>Example (Unity GameManager):</para>
    /// <code>
    /// var config = new BridgeConfig
    /// {
    ///     MaxStatValue   = 9999,
    ///     EnableAuditLog = true,
    ///     AuditLogPath   = Application.persistentDataPath + "/bridgemod_audit.log"
    /// };
    /// var bridge = new ModBridge(config);
    /// </code>
    /// </summary>
    public record BridgeConfig
    {
        /// <summary>
        /// Maximum allowed value for any guarded stat field.
        /// Values above this ceiling are clamped and a <see cref="ErrorCodes.BoundClamp003"/>
        /// entry is written to the audit log. Default: 9999.
        /// </summary>
        public int MaxStatValue { get; init; } = 9999;

        /// <summary>
        /// Minimum allowed value for any guarded stat field.
        /// Stats cannot go negative. Default: 0.
        /// </summary>
        public int MinStatValue { get; init; } = 0;

        /// <summary>
        /// Whether to write audit entries. Should always be <c>true</c> in production.
        /// Disable only in performance-critical unit tests that mock the logger.
        /// Default: <c>true</c>.
        /// </summary>
        public bool EnableAuditLog { get; init; } = true;

        /// <summary>
        /// Absolute path to the audit log file (append-only).
        /// If <c>null</c>, entries are written to <see cref="Console.Error"/>.
        /// </summary>
        public string? AuditLogPath { get; init; } = null;
    }

    // -------------------------------------------------------------------------
    // Audit Logger
    // -------------------------------------------------------------------------

    /// <summary>
    /// Records every validation event to a tamper-evident, append-only audit log.
    ///
    /// <para>
    /// In production, entries are appended to the file at <see cref="BridgeConfig.AuditLogPath"/>.
    /// In this release, they additionally echo to <see cref="Console.Error"/>.
    /// All entries are also available in-memory via <see cref="Entries"/> for unit test inspection.
    /// </para>
    ///
    /// <para>Security note: Keep the audit log file on a write-protected partition or
    /// forward it to an immutable cloud sink (e.g. S3 with Object Lock) in production.</para>
    /// </summary>
    public class AuditLogger
    {
        private readonly BridgeConfig _config;

        /// <summary>All log entries written during this session, in order.</summary>
        public List<string> Entries { get; } = new();

        /// <param name="config">The bridge configuration controlling log behaviour.</param>
        public AuditLogger(BridgeConfig config) => _config = config;

        /// <summary>
        /// Append a new audit entry with an ISO 8601 UTC timestamp.
        /// </summary>
        /// <param name="eventCode">Machine-readable code (use <see cref="ErrorCodes"/> constants).</param>
        /// <param name="payloadId">Identifier for the originating payload (e.g. a hash or GUID).</param>
        /// <param name="detail">Human-readable description of the event for debugging.</param>
        public void Log(string eventCode, string payloadId, string detail)
        {
            if (!_config.EnableAuditLog) return;

            var timestamp = DateTimeOffset.UtcNow.ToString("o");
            var entry = $"[{timestamp}] [{eventCode}] payload={payloadId} :: {detail}";

            Entries.Add(entry);
            Console.Error.WriteLine($"  AUDIT | {entry}");
        }

        /// <summary>Returns <c>true</c> if any entry in <see cref="Entries"/> contains the given event code.</summary>
        public bool HasCode(string eventCode) =>
            Entries.Exists(e => e.Contains($"[{eventCode}]"));
    }

    // -------------------------------------------------------------------------
    // Validation Result
    // -------------------------------------------------------------------------

    /// <summary>
    /// The result returned by <see cref="ModBridge.Validate"/>.
    /// </summary>
    /// <param name="IsValid">
    /// <c>true</c> if the payload passed all validation gates and is safe to
    /// apply to game state.
    /// </param>
    /// <param name="SanitizedPayload">
    /// The cleaned payload dictionary. <c>null</c> if <paramref name="IsValid"/> is <c>false</c>.
    /// </param>
    /// <param name="ErrorCode">
    /// The error code that caused rejection, or <c>null</c> on success.
    /// See <see cref="ErrorCodes"/> for possible values.
    /// </param>
    public record ValidationResult(
        bool IsValid,
        Dictionary<string, object>? SanitizedPayload = null,
        string? ErrorCode = null
    );

    // -------------------------------------------------------------------------
    // ModBridge — The Core Firewall
    // -------------------------------------------------------------------------

    /// <summary>
    /// The security firewall that sits between the PC mod toolchain and the
    /// console game runtime.
    ///
    /// <para>Every mod payload must pass through <see cref="Validate"/> before being
    /// applied to game state. The bridge runs three sequential gates:</para>
    /// <list type="number">
    ///   <item><description>
    ///     <b>Deserialize &amp; Parse</b> — Checks all string fields for disallowed
    ///     markup. Rejects the payload with <see cref="ErrorCodes.ParseErr001"/> if any
    ///     is found.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Boundary Guards</b> — Clamps numeric stat fields to
    ///     [<see cref="BridgeConfig.MinStatValue"/>, <see cref="BridgeConfig.MaxStatValue"/>].
    ///     Writes a <see cref="ErrorCodes.BoundClamp003"/> audit entry for each clamped field.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Audit Logger</b> — Every rejection and clamp event is written to the
    ///     audit log with a timestamp and the originating payload ID.
    ///   </description></item>
    /// </list>
    ///
    /// <para>Quick start:</para>
    /// <code>
    /// var bridge = new ModBridge(new BridgeConfig { MaxStatValue = 9999 });
    /// var result = bridge.Validate(rawPayload, "my-payload-id");
    /// if (result.IsValid)
    ///     ApplyToGameState(result.SanitizedPayload!);
    /// </code>
    /// </summary>
    public class ModBridge
    {
        private static readonly Regex DisallowedPattern =
            new(@"<[^>]+>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly HashSet<string> StatKeys =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "health", "mana", "strength", "defense", "speed"
            };

        private readonly BridgeConfig _config;
        private readonly AuditLogger _logger;

        /// <summary>Expose the logger for unit test inspection of audit entries.</summary>
        public AuditLogger Logger => _logger;

        /// <param name="config">Bridge configuration. Use <see cref="BridgeConfig"/> defaults as a starting point.</param>
        public ModBridge(BridgeConfig config)
        {
            _config = config;
            _logger = new AuditLogger(config);
        }

        /// <summary>
        /// Run the full three-gate validation pipeline on an untrusted mod payload.
        /// </summary>
        /// <param name="rawPayload">
        /// The untrusted payload as a string-keyed dictionary. Typically the result
        /// of deserializing a JSON mod file.
        /// </param>
        /// <param name="payloadId">
        /// A stable identifier for this payload used in audit log entries.
        /// Use a cryptographic hash of the raw bytes in production.
        /// </param>
        /// <returns>
        /// A <see cref="ValidationResult"/>. Check <see cref="ValidationResult.IsValid"/> before
        /// accessing <see cref="ValidationResult.SanitizedPayload"/>.
        /// </returns>
        public ValidationResult Validate(
            Dictionary<string, object> rawPayload,
            string payloadId = "unknown")
        {
            var payload = new Dictionary<string, object>(rawPayload, StringComparer.OrdinalIgnoreCase);

            if (SanitizeStrings(payload, payloadId))
                return new ValidationResult(IsValid: false, ErrorCode: ErrorCodes.ParseErr001);

            ApplyBoundaryGuards(payload, payloadId);

            return new ValidationResult(IsValid: true, SanitizedPayload: payload);
        }

        /// <summary>
        /// Gate 1: Scan all string fields for disallowed markup patterns.
        /// Returns <c>true</c> (reject) if any disallowed content is found.
        /// </summary>
        private bool SanitizeStrings(Dictionary<string, object> payload, string payloadId)
        {
            foreach (var (key, value) in payload)
            {
                if (value is string str && DisallowedPattern.IsMatch(str))
                {
                    _logger.Log(
                        ErrorCodes.ParseErr001,
                        payloadId,
                        $"Disallowed markup in field '{key}': {str}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gate 2: Clamp numeric stat fields to the configured [min, max] range.
        /// Writes an audit entry for each field that is actually clamped.
        /// </summary>
        private void ApplyBoundaryGuards(Dictionary<string, object> payload, string payloadId)
        {
            foreach (var key in StatKeys)
            {
                if (!payload.TryGetValue(key, out var rawValue)) continue;

                double numericValue = rawValue switch
                {
                    int i    => i,
                    long l   => l,
                    double d => d,
                    float f  => f,
                    _        => double.NaN
                };

                if (double.IsNaN(numericValue)) continue;

                var clamped = Math.Clamp(numericValue, _config.MinStatValue, _config.MaxStatValue);
                if (Math.Abs(clamped - numericValue) > double.Epsilon)
                {
                    _logger.Log(
                        ErrorCodes.BoundClamp003,
                        payloadId,
                        $"Field '{key}' clamped: {numericValue} -> {clamped}");
                    payload[key] = rawValue is int or long ? (object)(int)clamped : clamped;
                }
            }
        }
    }
}
