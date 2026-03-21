// =============================================================================
// ProceduralTests.cs — BridgeMod.SDK Phase 4: Procedural Control Layer Tests
// =============================================================================
// Tests for BridgeRandom (Xorshift32 PRNG) and ProceduralWeightTable (weight normalization).
// Validates: determinism, seed handling, range generation, weight normalization.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text.Json;
using BridgeMod.Bridge;
using BridgeMod.Bridge.BehaviorGraphs;
using BridgeMod.Bridge.Governance;
using BridgeMod.Bridge.Models;
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

    public class ModFactoryTests
    {
        [Fact]
        public void ModFactory_DeserializeSimpleMod_Success()
        {
            var json = @"{
                ""modId"": ""example_mod"",
                ""randomSeed"": 12345,
                ""weightTables"": {
                    ""loot"": {
                        ""rare"": 10,
                        ""common"": 100
                    }
                }
            }";

            var definition = ModFactory.DeserializeProceduralMod(json);

            Assert.Equal("example_mod", definition.ModId);
            Assert.Equal(12345u, definition.RandomSeed);
            Assert.Single(definition.WeightTables);
            Assert.Equal(2, definition.WeightTables["loot"].Count);
            Assert.Equal(10, definition.WeightTables["loot"]["rare"]);
            Assert.Equal(100, definition.WeightTables["loot"]["common"]);
        }

        [Fact]
        public void ModFactory_DeserializeMultipleTables_Success()
        {
            var json = @"{
                ""modId"": ""multi_table_mod"",
                ""randomSeed"": 42,
                ""weightTables"": {
                    ""loot"": {
                        ""rare"": 5,
                        ""common"": 50
                    },
                    ""npc_names"": {
                        ""male"": 50,
                        ""female"": 50
                    }
                }
            }";

            var definition = ModFactory.DeserializeProceduralMod(json);

            Assert.Equal("multi_table_mod", definition.ModId);
            Assert.Equal(2, definition.WeightTables.Count);
            Assert.True(definition.WeightTables.ContainsKey("loot"));
            Assert.True(definition.WeightTables.ContainsKey("npc_names"));
        }

        [Fact]
        public void ModFactory_CreateBridgeRandom_WithSeed()
        {
            var definition = new ModFactory.ProceduralModDefinition
            {
                ModId = "test",
                RandomSeed = 999
            };

            var rng = ModFactory.CreateBridgeRandom(definition);

            Assert.Equal(999u, rng.Seed);
            Assert.NotEqual(0u, rng.Next()); // Should not be stuck
        }

        [Fact]
        public void ModFactory_CreateBridgeRandom_WithAuditLogger()
        {
            var definition = new ModFactory.ProceduralModDefinition
            {
                ModId = "test",
                RandomSeed = 555
            };
            var logger = new AuditLogger(new BridgeConfig { EnableAuditLog = true });

            var rng = ModFactory.CreateBridgeRandom(definition, logger);

            Assert.Equal(555u, rng.Seed);
            Assert.True(logger.HasCode(ErrorCodes.ProcGen001), "Should log PROCEDURAL_GEN_001");
        }

        [Fact]
        public void ModFactory_CreateWeightTables_MultipleTablesWithRawWeights()
        {
            var definition = new ModFactory.ProceduralModDefinition
            {
                ModId = "weight_test",
                RandomSeed = 0,
                WeightTables = new Dictionary<string, Dictionary<string, double>>
                {
                    { "loot", new Dictionary<string, double> { { "rare", 10 }, { "common", 100 } } },
                    { "npc", new Dictionary<string, double> { { "male", 50 }, { "female", 50 } } }
                }
            };
            var config = new BridgeConfig();

            var tables = ModFactory.CreateWeightTables(definition, config);

            Assert.Equal(2, tables.Count);
            Assert.True(tables.ContainsKey("loot"));
            Assert.True(tables.ContainsKey("npc"));

            // Verify loot table weights are populated
            Assert.Equal(2, tables["loot"].Weights.Count);
            Assert.Equal(10, tables["loot"].Weights["rare"]);
            Assert.Equal(100, tables["loot"].Weights["common"]);
        }

        [Fact]
        public void ModFactory_Validate_ValidDefinition_Passes()
        {
            var definition = new ModFactory.ProceduralModDefinition
            {
                ModId = "valid_mod",
                RandomSeed = 42,
                WeightTables = new Dictionary<string, Dictionary<string, double>>
                {
                    { "loot", new Dictionary<string, double> { { "item1", 50 } } }
                }
            };

            var (isValid, errorMessage) = ModFactory.Validate(definition);

            Assert.True(isValid);
            Assert.Empty(errorMessage);
        }

        [Fact]
        public void ModFactory_Validate_EmptyModId_Fails()
        {
            var definition = new ModFactory.ProceduralModDefinition
            {
                ModId = "",
                WeightTables = new Dictionary<string, Dictionary<string, double>>
                {
                    { "loot", new Dictionary<string, double> { { "item1", 50 } } }
                }
            };

            var (isValid, errorMessage) = ModFactory.Validate(definition);

            Assert.False(isValid);
            Assert.Contains("ModId", errorMessage);
        }

        [Fact]
        public void ModFactory_Validate_EmptyWeightTable_Fails()
        {
            var definition = new ModFactory.ProceduralModDefinition
            {
                ModId = "bad_mod",
                WeightTables = new Dictionary<string, Dictionary<string, double>>
                {
                    { "loot", new Dictionary<string, double>() } // Empty table
                }
            };

            var (isValid, errorMessage) = ModFactory.Validate(definition);

            Assert.False(isValid);
            Assert.Contains("at least one item", errorMessage);
        }

        [Fact]
        public void ModFactory_Validate_NegativeWeight_Fails()
        {
            var definition = new ModFactory.ProceduralModDefinition
            {
                ModId = "negative_weight_mod",
                WeightTables = new Dictionary<string, Dictionary<string, double>>
                {
                    { "loot", new Dictionary<string, double> { { "item1", -50 } } }
                }
            };

            var (isValid, errorMessage) = ModFactory.Validate(definition);

            Assert.False(isValid);
            Assert.Contains("negative", errorMessage);
        }

        [Fact]
        public void ModFactory_Validate_NaNWeight_Fails()
        {
            var definition = new ModFactory.ProceduralModDefinition
            {
                ModId = "nan_weight_mod",
                WeightTables = new Dictionary<string, Dictionary<string, double>>
                {
                    { "loot", new Dictionary<string, double> { { "item1", double.NaN } } }
                }
            };

            var (isValid, errorMessage) = ModFactory.Validate(definition);

            Assert.False(isValid);
            Assert.Contains("invalid", errorMessage);
        }

        [Fact]
        public void ModFactory_SerializeRoundTrip_Deterministic()
        {
            var original = new ModFactory.ProceduralModDefinition
            {
                ModId = "roundtrip_test",
                RandomSeed = 777,
                WeightTables = new Dictionary<string, Dictionary<string, double>>
                {
                    { "loot", new Dictionary<string, double> { { "rare", 10 }, { "common", 100 } } }
                }
            };

            var json = ModFactory.SerializeProceduralMod(original);
            var deserialized = ModFactory.DeserializeProceduralMod(json);

            Assert.Equal(original.ModId, deserialized.ModId);
            Assert.Equal(original.RandomSeed, deserialized.RandomSeed);
            Assert.Equal(original.WeightTables.Count, deserialized.WeightTables.Count);
        }

        [Fact]
        public void ModFactory_DeserializeInvalidJson_ThrowsJsonException()
        {
            var invalidJson = @"{ invalid json ]";

            Assert.Throws<JsonException>(() => ModFactory.DeserializeProceduralMod(invalidJson));
        }

        [Fact]
        public void ModFactory_DeserializeNullJson_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ModFactory.DeserializeProceduralMod(null!));
            Assert.Throws<ArgumentException>(() => ModFactory.DeserializeProceduralMod(""));
        }
    }

    public class ThalamusIntegrationTests
    {
        [Fact]
        public void ThalamosJSON_Deserialization_CreatesValidProceduralMod()
        {
            // Simulate JSON from Thalamus visual IDE
            var thalamusJson = @"{
                ""modId"": ""thalamus_visual_mod"",
                ""randomSeed"": 54321,
                ""weightTables"": {
                    ""weapon_drops"": {
                        ""iron_sword"": 100,
                        ""steel_sword"": 50,
                        ""mythril_sword"": 10
                    },
                    ""spell_tiers"": {
                        ""novice"": 80,
                        ""apprentice"": 30,
                        ""expert"": 5
                    }
                }
            }";

            // Step 1: Deserialize from Thalamus JSON
            var definition = ModFactory.DeserializeProceduralMod(thalamusJson);

            Assert.Equal("thalamus_visual_mod", definition.ModId);
            Assert.Equal(54321u, definition.RandomSeed);
            Assert.Equal(2, definition.WeightTables.Count);

            // Step 2: Validate the definition
            var (isValid, error) = ModFactory.Validate(definition);
            Assert.True(isValid, error);

            // Step 3: Create BridgeRandom from the seed
            var rng = ModFactory.CreateBridgeRandom(definition);
            Assert.Equal(54321u, rng.Seed);

            // Step 4: Create weight tables
            var config = new BridgeConfig();
            var tables = ModFactory.CreateWeightTables(definition, config);

            // Step 5: Verify tables are usable
            var weaponNormalized = tables["weapon_drops"].GetNormalizedWeights();
            Assert.Equal(3, weaponNormalized.Count);

            // Check that probabilities sum to 1.0
            var sum = weaponNormalized["iron_sword"]
                + weaponNormalized["steel_sword"]
                + weaponNormalized["mythril_sword"];
            Assert.Equal(1.0, sum, precision: 5);

            // Step 6: Use RNG to select items
            var rng2 = ModFactory.CreateBridgeRandom(definition); // Same seed
            var selections = new Dictionary<string, int>();
            foreach (var item in weaponNormalized.Keys)
                selections[item] = 0;

            for (int i = 0; i < 100; i++)
            {
                var rand = rng2.NextFloat();
                var cumulative = 0.0;
                foreach (var (item, prob) in weaponNormalized)
                {
                    cumulative += prob;
                    if (rand <= cumulative)
                    {
                        selections[item]++;
                        break;
                    }
                }
            }

            // Verify distribution follows expectations (iron_sword should be most common)
            Assert.True(selections["iron_sword"] > selections["steel_sword"]);
            Assert.True(selections["steel_sword"] > selections["mythril_sword"]);
        }

        [Fact]
        public void GovernanceProvider_Interface_IsUsable()
        {
            // Verify that IGovernanceProvider can be implemented (even if not used here)
            var mockGovernance = new MockGovernanceProvider();

            Assert.True(mockGovernance.VerifyManifest("{}"));
            Assert.False(mockGovernance.VerifyManifest(null!));
            Assert.True(mockGovernance.IsAuthorizedForSurface("test_mod", "Data"));
        }

        /// <summary>Mock implementation of IGovernanceProvider for testing interface contract.</summary>
        private class MockGovernanceProvider : IGovernanceProvider
        {
            public bool VerifyManifest(string manifestJson)
            {
                return !string.IsNullOrWhiteSpace(manifestJson);
            }

            public System.Threading.Tasks.Task<string?> RequestAuditAsync(string modId)
            {
                return System.Threading.Tasks.Task.FromResult<string?>(null);
            }

            public bool IsAuthorizedForSurface(string modId, string surfaceCategory)
            {
                return true; // Mock always authorizes
            }
        }
    }

    /// <summary>
    /// Tests for BridgeMod v0.6.0 "Interface Alpha" — Project Manifest system,
    /// governance status tracking, and logic fingerprint engine.
    /// </summary>
    public class ManifestFingerprintTests
    {
        /// <summary>Helper to create a weight table and populate it with test data.</summary>
        private static ProceduralWeightTable MakeWeightTable(params (string key, double value)[] entries)
        {
            var table = new ProceduralWeightTable(new BridgeConfig());
            foreach (var (key, value) in entries)
                table.Weights[key] = value;
            return table;
        }

        [Fact]
        public void Fingerprint_DifferentWeights_ProduceDifferentHashes()
        {
            // Two tables differing by one weight value must produce different fingerprints
            var tables1 = new Dictionary<string, ProceduralWeightTable>
            {
                { "loot", MakeWeightTable(("rare", 10.0), ("common", 100.0)) }
            };
            var tables2 = new Dictionary<string, ProceduralWeightTable>
            {
                { "loot", MakeWeightTable(("rare", 99.0), ("common", 100.0)) }  // rare changed
            };

            var bridge = new ModBridge(new BridgeConfig());
            var fp1 = bridge.GenerateLogicFingerprint(tables: tables1);
            var fp2 = bridge.GenerateLogicFingerprint(tables: tables2);

            Assert.NotEqual(fp1, fp2);
        }

        [Fact]
        public void Fingerprint_IdenticalInputs_ProduceIdenticalHashes()
        {
            // Determinism test: calling twice with same data yields same result
            var tables = new Dictionary<string, ProceduralWeightTable>
            {
                { "loot", MakeWeightTable(("rare", 10.0), ("common", 100.0)) }
            };

            var bridge = new ModBridge(new BridgeConfig());
            var fp1 = bridge.GenerateLogicFingerprint(tables: tables);
            var fp2 = bridge.GenerateLogicFingerprint(tables: tables);

            Assert.Equal(fp1, fp2);
        }

        [Fact]
        public void Fingerprint_IsHexString_64Chars()
        {
            // SHA256 = 32 bytes = 64 hex characters
            var bridge = new ModBridge(new BridgeConfig());
            var fp = bridge.GenerateLogicFingerprint();

            Assert.Equal(64, fp.Length);
            Assert.Matches("^[0-9a-f]{64}$", fp);
        }

        [Fact]
        public void Fingerprint_EmptyInputs_ProducesConsistentHash()
        {
            var bridge = new ModBridge(new BridgeConfig());
            var fp1 = bridge.GenerateLogicFingerprint();
            var fp2 = bridge.GenerateLogicFingerprint();

            Assert.Equal(fp1, fp2);
        }

        [Fact]
        public void Fingerprint_WithSurfaces_DiffersFromNoSurfaces()
        {
            var bridge = new ModBridge(new BridgeConfig());
            var fpEmpty = bridge.GenerateLogicFingerprint();

            var registry = new ModSurfaceRegistry();
            registry.Register(new ModSurfaceDeclaration(
                "WeaponBalance", ModSurfaceCategory.Data, ModSurfaceStatus.Enabled, "Weapon stats"));

            var fpWithSurface = bridge.GenerateLogicFingerprint(registry: registry);
            Assert.NotEqual(fpEmpty, fpWithSurface);
        }

        [Fact]
        public void Fingerprint_WithBehaviorGraph_DiffersFromEmpty()
        {
            var bridge = new ModBridge(new BridgeConfig());
            var fpEmpty = bridge.GenerateLogicFingerprint();

            var graph = new BehaviorGraphDefinition(
                graphId: "enemy_ai",
                version: "1.0",
                states: new[] { new BehaviorState("idle"), new BehaviorState("attack") },
                initialStateId: "idle",
                transitions: new[] { new BehaviorTransition("idle", "attack", "spotted") }
            );

            var fpWithGraph = bridge.GenerateLogicFingerprint(graphs: new[] { graph });
            Assert.NotEqual(fpEmpty, fpWithGraph);
        }

        [Fact]
        public void GovernanceStatus_IsUngoverned_ByDefault()
        {
            var bridge = new ModBridge(new BridgeConfig());
            Assert.Equal(GovernanceStatus.Ungoverned, bridge.CurrentGovernanceStatus);
        }

        [Fact]
        public void GovernanceStatus_FlipsToGoverned_WhenCertificateSet()
        {
            var bridge = new ModBridge(new BridgeConfig());
            bridge.GovernanceCertificate = "KANON-CERT-ABC123";
            Assert.Equal(GovernanceStatus.GovernedByKanon, bridge.CurrentGovernanceStatus);
        }

        [Fact]
        public void GovernanceStatus_FlipsBackToUngoverned_WhenCertificateCleared()
        {
            var bridge = new ModBridge(new BridgeConfig());
            bridge.GovernanceCertificate = "KANON-CERT-ABC123";
            bridge.GovernanceCertificate = null;
            Assert.Equal(GovernanceStatus.Ungoverned, bridge.CurrentGovernanceStatus);
        }

        [Fact]
        public void AuditLog_EntriesArePrefixed_WithUngoverned_ByDefault()
        {
            var bridge = new ModBridge(new BridgeConfig { EnableAuditLog = true });

            // Trigger a validation event that generates a log entry
            var payload = new Dictionary<string, object> { { "health", 999999 } };
            bridge.Validate(payload, "test-payload");

            // All entries should start with [UNGOVERNED]
            Assert.True(bridge.Logger.Entries.Count > 0, "Expected at least one audit entry");
            foreach (var entry in bridge.Logger.Entries)
            {
                Assert.StartsWith("[UNGOVERNED]", entry);
            }
        }

        [Fact]
        public void AuditLog_EntriesArePrefixed_WithGoverned_WhenCertificateSet()
        {
            var bridge = new ModBridge(new BridgeConfig { EnableAuditLog = true });
            bridge.GovernanceCertificate = "KANON-CERT-XYZ";

            // Trigger a validation event
            var payload = new Dictionary<string, object> { { "health", 999999 } };
            bridge.Validate(payload, "test-payload");

            // All entries should start with [GOVERNED_BY_KANON]
            Assert.True(bridge.Logger.Entries.Count > 0, "Expected at least one audit entry");
            foreach (var entry in bridge.Logger.Entries)
            {
                Assert.StartsWith("[GOVERNED_BY_KANON]", entry);
            }
        }

        [Fact]
        public void AuditLog_HasCode_StillWorks_AfterPrefixChange()
        {
            // Regression test: HasCode() must work with new governance prefix format
            var bridge = new ModBridge(new BridgeConfig { EnableAuditLog = true });
            var payload = new Dictionary<string, object> { { "name", "<script>alert('xss')</script>" } };
            bridge.Validate(payload, "xss-test");

            // HasCode searches for "[PARSE_ERR_001]" substring
            Assert.True(bridge.Logger.HasCode(ErrorCodes.ParseErr001),
                "HasCode should find error code even with governance prefix");
        }

        [Fact]
        public void ProjectManifest_ToJson_ContainsAllRequiredFields()
        {
            var bridge = new ModBridge(new BridgeConfig());
            var manifest = bridge.GenerateManifest("test-proj", "0.6.0");

            var json = manifest.ToJson();

            // Verify all key fields are present
            Assert.Contains("\"ProjectId\"", json);
            Assert.Contains("\"test-proj\"", json);
            Assert.Contains("\"Version\"", json);
            Assert.Contains("\"0.6.0\"", json);
            Assert.Contains("\"LogicFingerprint\"", json);
            Assert.Contains("\"GovernanceStatus\"", json);
            Assert.Contains("\"ActiveSurfacesCount\"", json);
            Assert.Contains("\"Timestamp\"", json);
        }

        [Fact]
        public void ProjectManifest_ToJson_IsValidJson()
        {
            var bridge = new ModBridge(new BridgeConfig());
            var manifest = bridge.GenerateManifest("test-proj", "0.6.0");
            var json = manifest.ToJson();

            // Should parse without exception
            var doc = JsonDocument.Parse(json);
            Assert.NotNull(doc);
        }

        [Fact]
        public void ProjectManifest_GovernanceStatus_IsGoverned_WhenCertificatePresent()
        {
            var bridge = new ModBridge(new BridgeConfig());
            bridge.GovernanceCertificate = "KANON-CERT-123";
            var manifest = bridge.GenerateManifest("test-proj", "0.6.0");

            Assert.Equal(GovernanceStatus.GovernedByKanon, manifest.GovernanceStatus);
        }

        [Fact]
        public void GenerateManifest_ActiveSurfacesCount_MatchesRegistry()
        {
            var registry = new ModSurfaceRegistry();
            registry.Register(new ModSurfaceDeclaration(
                "WeaponBalance", ModSurfaceCategory.Data, ModSurfaceStatus.Enabled, "Weapon stats"));
            registry.Register(new ModSurfaceDeclaration(
                "ArmorBalance", ModSurfaceCategory.Data, ModSurfaceStatus.Enabled, "Armor stats"));

            var bridge = new ModBridge(new BridgeConfig());
            var manifest = bridge.GenerateManifest("test-proj", "0.6.0", registry: registry);

            Assert.Equal(2, manifest.ActiveSurfacesCount);
        }
    }
}
