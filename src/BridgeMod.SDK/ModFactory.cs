// =============================================================================
// ModFactory.cs — BridgeMod.SDK Procedural Mod Factory
// =============================================================================
// Copyright (c) 2024 DreamCraft: Legacies Project
// License: MIT (see LICENSE in repository root)
//
// Description:
//   Factory for deserializing mod definitions from JSON (e.g., from Project Thalamus).
//   Handles:
//   - BridgeRandom seed initialization
//   - ProceduralWeightTable distribution definitions
//   - Safe deserialization using System.Text.Json (zero external dependencies)
//
// Supported JSON format:
//   {
//     "modId": "example_mod",
//     "procedural": {
//       "randomSeed": 12345,
//       "weightTables": {
//         "loot_table": { "rare": 10, "common": 100, "epic": 50 }
//       }
//     }
//   }
//
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BridgeMod.Bridge.Procedural
{
    /// <summary>
    /// Factory for deserializing procedural mod definitions from JSON.
    /// Supports BridgeRandom seeds and ProceduralWeightTable distributions.
    /// Uses System.Text.Json for lightweight, dependency-free deserialization.
    /// </summary>
    public sealed class ModFactory
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Represents a deserialized procedural mod definition from JSON.
        /// </summary>
        public class ProceduralModDefinition
        {
            /// <summary>Unique mod identifier.</summary>
            public string ModId { get; set; } = string.Empty;

            /// <summary>Procedural generation seed. If 0, BridgeRandom will set it to 1.</summary>
            public uint RandomSeed { get; set; } = 0;

            /// <summary>
            /// Named weight tables. Key is table name; value is a dictionary of item names
            /// mapped to raw weights. Weights will be normalized to [0, 1] by ProceduralWeightTable.
            /// </summary>
            public Dictionary<string, Dictionary<string, double>> WeightTables { get; set; } = new();
        }

        /// <summary>
        /// Deserializes a procedural mod definition from JSON string.
        /// </summary>
        /// <param name="json">JSON blob defining the mod's procedural properties.</param>
        /// <returns>A deserialized <see cref="ProceduralModDefinition"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if json is null.</exception>
        /// <exception cref="JsonException">Thrown if JSON is malformed or invalid.</exception>
        public static ProceduralModDefinition DeserializeProceduralMod(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be null or whitespace.", nameof(json));

            try
            {
                var definition = JsonSerializer.Deserialize<ProceduralModDefinition>(json, JsonOptions)
                    ?? throw new JsonException("Deserialization returned null.");

                // Validate modId
                if (string.IsNullOrWhiteSpace(definition.ModId))
                    throw new JsonException("ModId must not be empty.");

                return definition;
            }
            catch (JsonException ex)
            {
                throw new JsonException($"Failed to deserialize procedural mod JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a BridgeRandom instance initialized with the procedural definition's seed.
        /// </summary>
        /// <param name="definition">The deserialized procedural mod definition.</param>
        /// <param name="auditLogger">Optional audit logger for seed initialization logging.</param>
        /// <returns>A new <see cref="BridgeRandom"/> instance seeded from the definition.</returns>
        /// <exception cref="ArgumentNullException">Thrown if definition is null.</exception>
        public static BridgeRandom CreateBridgeRandom(
            ProceduralModDefinition definition,
            AuditLogger? auditLogger = null)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            return new BridgeRandom(definition.RandomSeed, auditLogger);
        }

        /// <summary>
        /// Creates ProceduralWeightTable instances from the definition's weight tables.
        /// Each weight table is initialized with raw weights from the JSON definition.
        /// </summary>
        /// <param name="definition">The deserialized procedural mod definition.</param>
        /// <param name="config">Bridge configuration for clamping weights.</param>
        /// <returns>
        /// A dictionary mapping table names to initialized <see cref="ProceduralWeightTable"/> instances.
        /// Weights are ready for normalization via <see cref="ProceduralWeightTable.GetNormalizedWeights"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if definition or config is null.</exception>
        public static Dictionary<string, ProceduralWeightTable> CreateWeightTables(
            ProceduralModDefinition definition,
            BridgeConfig config)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var tables = new Dictionary<string, ProceduralWeightTable>(definition.WeightTables.Count);

            foreach (var (tableName, weights) in definition.WeightTables)
            {
                if (string.IsNullOrWhiteSpace(tableName))
                    continue; // Skip malformed table names

                var table = new ProceduralWeightTable(config);
                foreach (var (itemName, weight) in weights)
                {
                    if (!string.IsNullOrWhiteSpace(itemName))
                    {
                        table.Weights[itemName] = weight;
                    }
                }

                tables[tableName] = table;
            }

            return tables;
        }

        /// <summary>
        /// Validates a procedural mod definition against common sanity checks.
        /// </summary>
        /// <param name="definition">The procedural mod definition to validate.</param>
        /// <returns>
        /// A tuple of (isValid, errorMessage). If isValid is false, errorMessage contains the reason.
        /// </returns>
        public static (bool isValid, string errorMessage) Validate(ProceduralModDefinition definition)
        {
            if (definition == null)
                return (false, "Definition is null.");

            if (string.IsNullOrWhiteSpace(definition.ModId))
                return (false, "ModId must not be empty.");

            // Validate weight tables
            foreach (var (tableName, weights) in definition.WeightTables)
            {
                if (string.IsNullOrWhiteSpace(tableName))
                    return (false, "Weight table name must not be empty.");

                if (weights == null || weights.Count == 0)
                    return (false, $"Weight table '{tableName}' must contain at least one item.");

                // Check for non-negative weights
                foreach (var (itemName, weight) in weights)
                {
                    if (string.IsNullOrWhiteSpace(itemName))
                        return (false, $"Item name in table '{tableName}' must not be empty.");

                    if (double.IsNaN(weight) || double.IsInfinity(weight))
                        return (false, $"Weight for '{itemName}' in table '{tableName}' is invalid (NaN or Infinity).");

                    if (weight < 0)
                        return (false, $"Weight for '{itemName}' in table '{tableName}' cannot be negative.");
                }
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Serializes a procedural mod definition back to JSON string.
        /// Useful for debugging, logging, or round-trip validation.
        /// </summary>
        /// <param name="definition">The procedural mod definition to serialize.</param>
        /// <returns>A JSON string representation of the definition.</returns>
        /// <exception cref="ArgumentNullException">Thrown if definition is null.</exception>
        public static string SerializeProceduralMod(ProceduralModDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            return JsonSerializer.Serialize(definition, JsonOptions);
        }
    }
}
