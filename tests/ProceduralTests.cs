// =============================================================================
// ProceduralTests.cs — BridgeMod.SDK Phase 4: Procedural Control Layer Tests
// =============================================================================
// Tests for BridgeRandom (Xorshift32 PRNG) and ProceduralWeightTable (weight normalization).
// Validates: determinism, seed handling, range generation, weight normalization.
// =============================================================================

using System;
using System.Collections.Generic;
using BridgeMod.Bridge;
using BridgeMod.Bridge.Procedural;
using Xunit;

namespace BridgeMod.Tests
{
    public class BridgeRandomTests
    {
        [Fact]
        public void BridgeRandom_DeterministicSequence_ThousandNumbers()
        {
            // Two instances with the same seed must produce identical sequences
            var rng1 = new BridgeRandom(42u);
            var rng2 = new BridgeRandom(42u);

            for (int i = 0; i < 1000; i++)
            {
                Assert.Equal(rng1.Next(), rng2.Next());
            }
        }

        [Fact]
        public void BridgeRandom_DifferentSeeds_ProduceDifferentSequences()
        {
            var rng1 = new BridgeRandom(1u);
            var rng2 = new BridgeRandom(2u);

            int differences = 0;
            for (int i = 0; i < 100; i++)
            {
                if (rng1.Next() != rng2.Next())
                    differences++;
            }

            // Highly unlikely (virtually impossible) for 100 sequences to be identical
            Assert.True(differences > 50, "Different seeds should produce different sequences");
        }

        [Fact]
        public void BridgeRandom_SeedZero_FallsBackToOne_DoesNotGetStuck()
        {
            // Xorshift32(0) returns 0 forever; seed=0 must fall back to 1
            var rng = new BridgeRandom(0u);

            // Seed property should expose the effective seed (1, not 0)
            Assert.Equal(1u, rng.Seed);

            // Generate 100 values; none should be zero (proves it doesn't get stuck)
            for (int i = 0; i < 100; i++)
            {
                Assert.NotEqual(0u, rng.Next());
            }
        }

        [Fact]
        public void BridgeRandom_Seed_ExposesEffectiveSeed()
        {
            var rng1 = new BridgeRandom(5u);
            Assert.Equal(5u, rng1.Seed);

            var rng2 = new BridgeRandom(0u);
            Assert.Equal(1u, rng2.Seed); // seed=0 converts to 1
        }

        [Fact]
        public void BridgeRandom_NextFloat_AlwaysInUnitRange()
        {
            var rng = new BridgeRandom(123u);

            for (int i = 0; i < 1000; i++)
            {
                double val = rng.NextFloat();
                Assert.True(val >= 0.0 && val <= 1.0, $"NextFloat() returned {val}, expected [0.0, 1.0]");
            }
        }

        [Fact]
        public void BridgeRandom_NextRange_AlwaysInBounds()
        {
            var rng = new BridgeRandom(456u);

            // Test multiple ranges
            for (int i = 0; i < 100; i++)
            {
                int val = rng.NextRange(0, 10);
                Assert.True(val >= 0 && val < 10, $"NextRange(0, 10) returned {val}");
            }

            rng = new BridgeRandom(789u);
            for (int i = 0; i < 100; i++)
            {
                int val = rng.NextRange(-5, 5);
                Assert.True(val >= -5 && val < 5, $"NextRange(-5, 5) returned {val}");
            }
        }

        [Fact]
        public void BridgeRandom_NextRange_InvalidArgs_Throws()
        {
            var rng = new BridgeRandom(999u);

            // min >= max should throw ArgumentException
            Assert.Throws<ArgumentException>(() => rng.NextRange(10, 10));
            Assert.Throws<ArgumentException>(() => rng.NextRange(10, 5));
        }

        [Fact]
        public void BridgeRandom_LogsProcGen001_WhenAuditLoggerProvided()
        {
            var logger = new AuditLogger(new BridgeConfig { EnableAuditLog = true });

            // Create BridgeRandom with logger
            var rng = new BridgeRandom(12345u, logger);

            // Logger should have a ProcGen001 entry
            Assert.True(logger.HasCode(ErrorCodes.ProcGen001), "Expected PROCEDURAL_GEN_001 audit entry");

            // Entry should mention the seed
            var entry = logger.Entries.Find(e => e.Contains(ErrorCodes.ProcGen001));
            Assert.NotNull(entry);
            Assert.Contains("12345", entry);
        }
    }

    public class ProceduralWeightTableTests
    {
        [Fact]
        public void WeightTable_SimpleWeights_NormalizesCorrectly()
        {
            var config = new BridgeConfig { MinStatValue = 0, MaxStatValue = 9999 };
            var table = new ProceduralWeightTable(config);

            // Set raw weights
            table.Weights["a"] = 10;
            table.Weights["b"] = 10;
            table.Weights["c"] = 20;

            var normalized = table.GetNormalizedWeights();

            // Expected: {a: 0.25, b: 0.25, c: 0.5}
            Assert.Equal(3, normalized.Count);
            Assert.Equal(0.25, normalized["a"], precision: 5);
            Assert.Equal(0.25, normalized["b"], precision: 5);
            Assert.Equal(0.5, normalized["c"], precision: 5);
        }

        [Fact]
        public void WeightTable_LargeWeights_AreClamped()
        {
            var config = new BridgeConfig { MinStatValue = 0, MaxStatValue = 100 };
            var table = new ProceduralWeightTable(config);

            // Set weights, one of which exceeds MaxStatValue
            table.Weights["normal"] = 50;
            table.Weights["huge"] = 999999; // Will be clamped to 100

            var normalized = table.GetNormalizedWeights();

            // After clamping: normal=50, huge=100; sum=150
            // Normalized: normal=50/150≈0.333, huge=100/150≈0.667
            Assert.Equal(2, normalized.Count);
            Assert.Equal(50.0 / 150.0, normalized["normal"], precision: 5);
            Assert.Equal(100.0 / 150.0, normalized["huge"], precision: 5);

            // Verify sum is 1.0
            double sum = normalized["normal"] + normalized["huge"];
            Assert.Equal(1.0, sum, precision: 5);
        }

        [Fact]
        public void WeightTable_EmptyWeights_ReturnsEmpty()
        {
            var config = new BridgeConfig();
            var table = new ProceduralWeightTable(config);

            // Don't add any weights
            var normalized = table.GetNormalizedWeights();

            Assert.Empty(normalized);
        }

        [Fact]
        public void WeightTable_SingleItem_ReturnsOne()
        {
            var config = new BridgeConfig();
            var table = new ProceduralWeightTable(config);

            table.Weights["only"] = 42;

            var normalized = table.GetNormalizedWeights();

            Assert.Single(normalized);
            Assert.Equal(1.0, normalized["only"], precision: 5);
        }

        [Fact]
        public void WeightTable_AllZeroWeights_DistributesEqually()
        {
            var config = new BridgeConfig { MinStatValue = 0, MaxStatValue = 9999 };
            var table = new ProceduralWeightTable(config);

            // Set all weights to zero (or below MinStatValue)
            table.Weights["a"] = 0;
            table.Weights["b"] = 0;
            table.Weights["c"] = 0;

            var normalized = table.GetNormalizedWeights();

            // Should distribute equally: 1/3 each
            Assert.Equal(3, normalized.Count);
            double expected = 1.0 / 3.0;
            Assert.Equal(expected, normalized["a"], precision: 5);
            Assert.Equal(expected, normalized["b"], precision: 5);
            Assert.Equal(expected, normalized["c"], precision: 5);

            // Verify sum is 1.0
            double sum = normalized["a"] + normalized["b"] + normalized["c"];
            Assert.Equal(1.0, sum, precision: 5);
        }

        [Fact]
        public void WeightTable_NormalizationAlwaysSumsToOne()
        {
            var config = new BridgeConfig { MinStatValue = 0, MaxStatValue = 9999 };
            var table = new ProceduralWeightTable(config);

            // Add diverse weights
            table.Weights["w1"] = 1;
            table.Weights["w2"] = 5;
            table.Weights["w3"] = 10;
            table.Weights["w4"] = 100;
            table.Weights["w5"] = 0.001;

            var normalized = table.GetNormalizedWeights();

            // Sum all normalized weights
            double sum = 0;
            foreach (var val in normalized.Values) sum += val;

            Assert.Equal(1.0, sum, precision: 5);
        }

        [Fact]
        public void WeightTable_NegativeWeights_AreClamped()
        {
            var config = new BridgeConfig { MinStatValue = 0, MaxStatValue = 9999 };
            var table = new ProceduralWeightTable(config);

            table.Weights["negative"] = -100; // Will be clamped to 0
            table.Weights["positive"] = 100;

            var normalized = table.GetNormalizedWeights();

            // After clamping: negative=0, positive=100; sum=100
            // Normalized: negative=0/100=0, positive=100/100=1
            Assert.Equal(2, normalized.Count);
            Assert.Equal(0.0, normalized["negative"], precision: 5);
            Assert.Equal(1.0, normalized["positive"], precision: 5);
        }
    }
}
