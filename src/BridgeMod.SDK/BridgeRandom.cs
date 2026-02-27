// =============================================================================
// BridgeRandom.cs — BridgeMod.SDK Phase 4: Procedural Control Layer
// =============================================================================
// Copyright (c) 2024 DreamCraft: Legacies Project
// License: MIT (see LICENSE in repository root)
//
// Description:
//   Deterministic pseudo-random number generator using Xorshift32.
//   No platform-specific libraries. Same seed always produces the same sequence.
//   Intended for mod-authored procedural generation with determinism guarantee.
//
// =============================================================================

using System;
using BridgeMod.Bridge;

namespace BridgeMod.Bridge.Procedural
{
    /// <summary>
    /// Deterministic pseudo-random number generator using the Xorshift32 algorithm.
    /// Same seed always produces the same sequence. No platform-specific libraries.
    /// </summary>
    public sealed class BridgeRandom
    {
        private uint _state;

        /// <summary>The effective seed used to initialize this instance.</summary>
        public uint Seed { get; }

        /// <summary>
        /// Initializes a new BridgeRandom with the specified seed.
        /// If seed is 0, it is automatically incremented to 1 (Xorshift32 invariant).
        /// </summary>
        /// <param name="seed">Deterministic seed. Use the same seed to reproduce a sequence.</param>
        /// <param name="auditLogger">
        /// Optional audit logger. When provided, logs <see cref="ErrorCodes.ProcGen001"/>
        /// with the effective seed on initialization.
        /// </param>
        public BridgeRandom(uint seed, AuditLogger? auditLogger = null)
        {
            _state = seed == 0u ? 1u : seed;
            Seed = _state;

            auditLogger?.Log(
                ErrorCodes.ProcGen001,
                "BridgeRandom",
                $"Initialized with seed {Seed}");
        }

        /// <summary>Returns the next pseudo-random uint.</summary>
        public uint Next()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        /// <summary>Returns the next pseudo-random double in [0.0, 1.0].</summary>
        public double NextFloat() => Next() / (double)uint.MaxValue;

        /// <summary>
        /// Returns a pseudo-random int in [<paramref name="min"/>, <paramref name="max"/>).
        /// </summary>
        /// <param name="min">Inclusive lower bound.</param>
        /// <param name="max">Exclusive upper bound. Must be greater than <paramref name="min"/>.</param>
        public int NextRange(int min, int max)
        {
            if (min >= max)
                throw new ArgumentException(
                    $"min ({min}) must be less than max ({max}).", nameof(min));

            return min + (int)(NextFloat() * (max - min));
        }
    }
}
