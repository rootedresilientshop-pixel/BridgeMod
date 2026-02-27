// =============================================================================
// ProceduralWeightTable.cs — BridgeMod.SDK Phase 4: Procedural Control Layer
// =============================================================================
// Copyright (c) 2024 DreamCraft: Legacies Project
// License: MIT (see LICENSE in repository root)
//
// Description:
//   Named weight table that normalizes raw weights to a probability distribution.
//   Raw weights are clamped by BridgeConfig bounds before normalization,
//   ensuring a consistent, safe probability distribution for procedural selection.
//
// =============================================================================

using System;
using System.Collections.Generic;
using BridgeMod.Bridge;

namespace BridgeMod.Bridge.Procedural
{
    /// <summary>
    /// A named weight table that normalizes raw weights to a probability distribution.
    /// Raw weights are clamped by <see cref="BridgeConfig"/> bounds before normalization.
    /// </summary>
    public sealed class ProceduralWeightTable
    {
        private readonly BridgeConfig _config;

        /// <summary>Raw named weights. Populate before calling <see cref="GetNormalizedWeights"/>.</summary>
        public Dictionary<string, double> Weights { get; } = new Dictionary<string, double>();

        /// <param name="config">The bridge configuration providing clamp bounds.</param>
        public ProceduralWeightTable(BridgeConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Returns a normalized probability distribution derived from <see cref="Weights"/>.
        /// Each weight is first clamped to [MinStatValue, MaxStatValue], then divided by the
        /// total sum so all values sum to 1.0.
        /// </summary>
        /// <returns>
        /// An immutable dictionary mapping item names to normalized weights [0.0, 1.0].
        /// Returns empty if <see cref="Weights"/> is empty.
        /// </returns>
        public IReadOnlyDictionary<string, double> GetNormalizedWeights()
        {
            if (Weights.Count == 0)
                return new Dictionary<string, double>();

            // Gate 1: Clamp each weight to configured bounds
            var clamped = new Dictionary<string, double>(Weights.Count);
            foreach (var kvp in Weights)
            {
                clamped[kvp.Key] = Math.Clamp(kvp.Value, _config.MinStatValue, _config.MaxStatValue);
            }

            // Gate 2: Sum and normalize
            double sum = 0;
            foreach (var w in clamped.Values) sum += w;

            var normalized = new Dictionary<string, double>(clamped.Count);
            if (sum <= 0)
            {
                // All weights clamped to zero: distribute equally
                double equal = 1.0 / clamped.Count;
                foreach (var key in clamped.Keys)
                    normalized[key] = equal;
            }
            else
            {
                foreach (var kvp in clamped)
                    normalized[kvp.Key] = kvp.Value / sum;
            }

            return normalized;
        }
    }
}
