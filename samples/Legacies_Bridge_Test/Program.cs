// =============================================================================
// Program.cs — BridgeMod.SDK v0.2.4 — Sample Runner
// =============================================================================
// Copyright (c) 2024 DreamCraft: Legacies Project
// License: MIT (see LICENSE in repository root)
//
// Description:
//   This file is the runnable sample for the Legacies_Bridge_Test project.
//   It demonstrates all three validation gates of the BridgeMod.SDK firewall
//   using concrete, real-world payloads:
//
//     Test Case 1 — Standard Payload       (valid data, no audit entries)
//     Test Case 2 — Malicious Rejection    (script injection → PARSE_ERR_001)
//     Test Case 3 — Boundary Guard Clamp   (stat overflow → BOUND_CLAMP_003)
//
//   The library code (ModBridge, BridgeConfig, AuditLogger, ValidationResult,
//   ErrorCodes) lives in BridgeMod.Bridge.cs at the repository root and is
//   referenced via the BridgeMod.SDK.csproj project reference.
//
//   Read this file alongside pulse_test.py, which implements the same three
//   test cases in Python on the trusted engine side.
//
// How to Run:
//   dotnet run --project Legacies_Bridge_Test
// =============================================================================

using System;
using System.Collections.Generic;
using BridgeMod.Bridge;

namespace BridgeMod.Bridge.Sample
{
    /// <summary>
    /// Entry point for the Legacies_Bridge_Test sample.
    /// Runs the three canonical verification cases from the README table.
    /// </summary>
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine(new string('=', 60));
            Console.WriteLine("BridgeMod.SDK v0.2.4 — C# Bridge Sample Runner");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine();

            // Shared config — matches the Python engine's constants in pulse_test.py
            var config = new BridgeConfig
            {
                MaxStatValue   = 9999,
                EnableAuditLog = true,
            };

            RunStandardPayload(config);
            RunMaliciousPayload(config);
            RunBoundsOverflow(config);

            Console.WriteLine();
            Console.WriteLine("All sample cases complete.");
            Console.WriteLine("See pulse_test.py for the full pytest suite (python side).");
        }

        // ------------------------------------------------------------------
        // Test Case 1: Standard Payload — Happy Path
        // ------------------------------------------------------------------

        /// <summary>
        /// A well-formed mod payload with all values in range.
        /// Expected: IsValid=true, zero audit entries.
        /// </summary>
        private static void RunStandardPayload(BridgeConfig config)
        {
            Console.WriteLine("--- Test Case 1: Standard Payload ---");

            var bridge = new ModBridge(config);
            var result = bridge.Validate(
                new Dictionary<string, object>
                {
                    ["character_name"]  = "Aldric the Wanderer",
                    ["character_class"] = "Ranger",
                    ["health"]          = 250,
                    ["mana"]            = 100,
                    ["strength"]        = 14,
                },
                payloadId: "test-001"
            );

            PrintResult(result, bridge,
                expectedValid: true,
                checkField: ("health", 250));
        }

        // ------------------------------------------------------------------
        // Test Case 2: Malicious Payload — Script Injection
        // ------------------------------------------------------------------

        /// <summary>
        /// A payload where a character name field contains an HTML script tag.
        /// Expected: IsValid=false, ErrorCode=PARSE_ERR_001, audit entry written.
        /// </summary>
        private static void RunMaliciousPayload(BridgeConfig config)
        {
            Console.WriteLine("--- Test Case 2: Malicious Payload Rejection ---");

            var bridge = new ModBridge(config);
            var result = bridge.Validate(
                new Dictionary<string, object>
                {
                    // Attempt to inject executable markup in a display-name field.
                    ["character_name"] = "Aldric<script>alert('pwned')</script>",
                    ["health"]         = 100,
                },
                payloadId: "test-002"
            );

            Console.WriteLine($"  IsValid    : {result.IsValid}");       // Expected: False
            Console.WriteLine($"  ErrorCode  : {result.ErrorCode}");      // Expected: PARSE_ERR_001
            Console.WriteLine($"  Audit log  : {bridge.Logger.HasCode(ErrorCodes.ParseErr001)}"); // Expected: True
            bool pass = !result.IsValid && result.ErrorCode == ErrorCodes.ParseErr001;
            Console.WriteLine($"  RESULT     : {(pass ? "PASS" : "FAIL")}");
            Console.WriteLine();
        }

        // ------------------------------------------------------------------
        // Test Case 3: Boundary Guard — Stat Overflow
        // ------------------------------------------------------------------

        /// <summary>
        /// A payload where health is set to 999999 — far above the 9999 ceiling.
        /// Expected: IsValid=true, health clamped to 9999, BOUND_CLAMP_003 logged.
        /// </summary>
        private static void RunBoundsOverflow(BridgeConfig config)
        {
            Console.WriteLine("--- Test Case 3: Boundary Guard — Stat Overflow ---");

            var bridge = new ModBridge(config);
            var result = bridge.Validate(
                new Dictionary<string, object>
                {
                    ["character_name"] = "Overpowered Hero",
                    ["health"]         = 999999,
                    ["strength"]       = 50,
                },
                payloadId: "test-003"
            );

            PrintResult(result, bridge,
                expectedValid: true,
                checkField: ("health", 9999));
        }

        // ------------------------------------------------------------------
        // Shared Output Helper
        // ------------------------------------------------------------------

        private static void PrintResult(
            ValidationResult result,
            ModBridge bridge,
            bool expectedValid,
            (string Key, int Expected) checkField)
        {
            Console.WriteLine($"  IsValid    : {result.IsValid}");

            if (result.SanitizedPayload?.TryGetValue(checkField.Key, out var actual) == true)
                Console.WriteLine($"  {checkField.Key,-10} : {actual}  (expected {checkField.Expected})");

            Console.WriteLine($"  Audit log  : {bridge.Logger.Entries.Count} entries");

            bool pass = result.IsValid == expectedValid
                && (result.SanitizedPayload == null
                    || !result.SanitizedPayload.TryGetValue(checkField.Key, out var v)
                    || Convert.ToInt32(v) == checkField.Expected);

            Console.WriteLine($"  RESULT     : {(pass ? "PASS" : "FAIL")}");
            Console.WriteLine();
        }
    }
}
