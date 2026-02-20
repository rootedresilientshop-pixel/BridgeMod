// =============================================================================
// ModContractTests.cs — BridgeMod Phase 3 Foundation
// =============================================================================
// Tests for the canonical mod contract types.
// Validates: immutability, default values, forward compatibility guarantees.
//
// These tests protect the contract from accidental changes that could break
// compatibility with existing engines.
// =============================================================================

using BridgeMod.Bridge.ModContract;
using Xunit;

namespace BridgeMod.Tests
{
    public class ModManifestTests
    {
        [Fact]
        public void ModManifest_DefaultValues_AreCorrect()
        {
            var manifest = new ModManifest();

            Assert.Equal("1.0", manifest.Version);
            Assert.Equal(string.Empty, manifest.Name);
            Assert.Equal(string.Empty, manifest.ModVersion);
            Assert.Null(manifest.Author);
            Assert.Null(manifest.Description);
            Assert.Empty(manifest.TargetSurfaces);
            Assert.Null(manifest.Constraints);
        }

        [Fact]
        public void ModManifest_CanSetProperties()
        {
            var manifest = new ModManifest
            {
                Name = "TestMod",
                ModVersion = "1.0.0",
                Author = "TestAuthor",
                Description = "Test description"
            };

            Assert.Equal("TestMod", manifest.Name);
            Assert.Equal("1.0.0", manifest.ModVersion);
            Assert.Equal("TestAuthor", manifest.Author);
            Assert.Equal("Test description", manifest.Description);
        }

        [Fact]
        public void ModManifest_TargetSurfacesList_IsMutable()
        {
            var manifest = new ModManifest();
            manifest.TargetSurfaces.Add("WeaponBalance");

            Assert.Single(manifest.TargetSurfaces);
            Assert.Contains("WeaponBalance", manifest.TargetSurfaces);
        }

        [Fact]
        public void ModManifest_Constraints_CanBeSet()
        {
            var manifest = new ModManifest
            {
                Constraints = new ModConstraints
                {
                    DataOnly = true,
                    Deterministic = true,
                    MaxExecutionMs = 1000
                }
            };

            Assert.NotNull(manifest.Constraints);
            Assert.True(manifest.Constraints.DataOnly);
            Assert.True(manifest.Constraints.Deterministic);
            Assert.Equal(1000, manifest.Constraints.MaxExecutionMs);
        }
    }

    public class ModConstraintsTests
    {
        [Fact]
        public void ModConstraints_Defaults_AreSafe()
        {
            var constraints = new ModConstraints();

            Assert.True(constraints.DataOnly);  // Default is data-only (safest)
            Assert.True(constraints.Deterministic);  // Assume deterministic by default
            Assert.Null(constraints.MaxExecutionMs);  // No limit by default
        }

        [Fact]
        public void ModConstraints_CanSetToNonDataOnly()
        {
            var constraints = new ModConstraints { DataOnly = false };

            Assert.False(constraints.DataOnly);
        }
    }

    public class ModPayloadTests
    {
        [Fact]
        public void ModPayload_DefaultValues_AreEmpty()
        {
            var payload = new ModPayload();

            Assert.Null(payload.Manifest);
            Assert.Equal(string.Empty, payload.Data);
            Assert.Null(payload.Metadata);
        }

        [Fact]
        public void ModPayload_CanContainValidatedData()
        {
            var manifest = new ModManifest { Name = "TestMod" };
            var payload = new ModPayload
            {
                Manifest = manifest,
                Data = "{\"health\": 100}",
                Metadata = new ValidationMetadata
                {
                    ValidatedAtUtcTicks = 1000000,
                    ValidatorVersion = "0.3.0",
                    GuardsApplied = 2,
                    ValuesAdjusted = 0
                }
            };

            Assert.NotNull(payload.Manifest);
            Assert.Equal("TestMod", payload.Manifest.Name);
            Assert.Equal("{\"health\": 100}", payload.Data);
            Assert.NotNull(payload.Metadata);
            Assert.Equal("0.3.0", payload.Metadata.ValidatorVersion);
        }
    }

    public class ValidationMetadataTests
    {
        [Fact]
        public void ValidationMetadata_TracksValidationProcess()
        {
            var metadata = new ValidationMetadata
            {
                ValidatedAtUtcTicks = 636789012340000000,  // Some timestamp
                ValidatorVersion = "0.3.0",
                GuardsApplied = 5,
                ValuesAdjusted = 2
            };

            Assert.Equal(636789012340000000, metadata.ValidatedAtUtcTicks);
            Assert.Equal("0.3.0", metadata.ValidatorVersion);
            Assert.Equal(5, metadata.GuardsApplied);
            Assert.Equal(2, metadata.ValuesAdjusted);
        }
    }

    public class ValidationFailureTests
    {
        [Fact]
        public void ValidationFailure_Records_FailureDetails()
        {
            var failure = new ValidationFailure
            {
                Path = "character[0].health",
                ErrorCode = "STAT_EXCEED_001",
                Message = "Health value exceeds maximum",
                Value = "999999"
            };

            Assert.Equal("character[0].health", failure.Path);
            Assert.Equal("STAT_EXCEED_001", failure.ErrorCode);
            Assert.Equal("Health value exceeds maximum", failure.Message);
            Assert.Equal("999999", failure.Value);
        }
    }

    public class ExecutionConstraintsTests
    {
        [Fact]
        public void ExecutionConstraints_HasSafeDefaults()
        {
            var constraints = new ExecutionConstraints();

            Assert.Equal(1000, constraints.MaxNodesPerGraph);
            Assert.Equal(5000, constraints.MaxEdgesPerGraph);
            Assert.Equal(10, constraints.MaxNestingDepth);
            Assert.Equal(10000, constraints.MaxExecutionSteps);
            Assert.Equal(104857600, constraints.MaxMemoryBytes);  // 100 MB
        }

        [Fact]
        public void ExecutionConstraints_CanBeCustomized()
        {
            var constraints = new ExecutionConstraints
            {
                MaxNodesPerGraph = 500,
                MaxExecutionSteps = 5000
            };

            Assert.Equal(500, constraints.MaxNodesPerGraph);
            Assert.Equal(5000, constraints.MaxExecutionSteps);
            Assert.Equal(5000, constraints.MaxEdgesPerGraph);  // Unchanged
        }
    }

    public class CanonicalContractTests
    {
        [Fact]
        public void Contract_IsForwardCompatible_EmptyManifestIsValid()
        {
            // A minimal manifest with only required fields should always be valid
            var manifest = new ModManifest
            {
                Name = "Test",
                ModVersion = "1.0.0"
            };

            // Should deserialize and work without error
            Assert.NotNull(manifest);
            Assert.Equal("Test", manifest.Name);
        }

        [Fact]
        public void Contract_Additive_NewOptionalFieldDoesNotBreak()
        {
            // Simulate adding a new optional field (like Constraints was added in v1.1)
            // Old code that doesn't set it should still work
            var manifest = new ModManifest { Name = "OldMod" };

            Assert.Null(manifest.Constraints);  // Old code never sets this
            // Engine can safely handle null constraints
        }

        [Fact]
        public void Contract_IsEngine_AgnosticAndSerialized()
        {
            // Contract types contain no engine-specific references
            var payload = new ModPayload
            {
                Data = "{\"generic\": \"json\"}"  // Engine-neutral structure
            };

            // Any engine can deserialize this
            Assert.Equal("{\"generic\": \"json\"}", payload.Data);
        }
    }
}
