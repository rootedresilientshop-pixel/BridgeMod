using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BridgeMod.Data;
using BridgeMod.Runtime;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BridgeMod.Tests
{
    public class ModManifestTests
    {
        [Fact]
        public void ValidManifest_ShouldPassValidation()
        {
            var manifest = new ModManifest
            {
                Name = "TestMod",
                Version = "1.0.0",
                Author = "TestAuthor",
                ModType = ModType.Data,
                Files = new[] { "data/config.json" }
            };

            var isValid = manifest.IsValid(out var errors);

            Assert.True(isValid);
            Assert.Empty(errors);
        }

        [Fact]
        public void ManifestWithoutName_ShouldFail()
        {
            var manifest = new ModManifest
            {
                Name = "",
                Version = "1.0.0",
                Author = "TestAuthor",
                ModType = ModType.Data,
                Files = new[] { "data/config.json" }
            };

            var isValid = manifest.IsValid(out var errors);

            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("name"));
        }

        [Fact]
        public void ManifestWithInvalidVersion_ShouldFail()
        {
            var manifest = new ModManifest
            {
                Name = "TestMod",
                Version = "invalid",
                Author = "TestAuthor",
                ModType = ModType.Data,
                Files = new[] { "data/config.json" }
            };

            var isValid = manifest.IsValid(out var errors);

            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("version") || e.Contains("semantic"));
        }

        [Fact]
        public void ManifestWithEmptyFiles_ShouldFail()
        {
            var manifest = new ModManifest
            {
                Name = "TestMod",
                Version = "1.0.0",
                Author = "TestAuthor",
                ModType = ModType.Data,
                Files = new string[] { }
            };

            var isValid = manifest.IsValid(out var errors);

            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("files"));
        }
    }

    public class ModVersionTests
    {
        [Fact]
        public void ValidSemanticVersion_ShouldParse()
        {
            var result = ModVersion.TryParse("1.2.3", out var version);

            Assert.True(result);
            Assert.NotNull(version);
            Assert.Equal(1, version.Major);
            Assert.Equal(2, version.Minor);
            Assert.Equal(3, version.Patch);
        }

        [Fact]
        public void InvalidVersion_ShouldNotParse()
        {
            var result = ModVersion.TryParse("invalid", out var version);

            Assert.False(result);
            Assert.Null(version);
        }

        [Fact]
        public void CompatibilityCheck_ShouldWork()
        {
            var v1_2_0 = ModVersion.TryParse("1.2.0", out var version1) ? version1! : null;
            var v1_0_0 = ModVersion.TryParse("1.0.0", out var version2) ? version2! : null;

            Assert.NotNull(v1_2_0);
            Assert.NotNull(v1_0_0);
            Assert.True(v1_2_0.IsCompatibleWith(v1_0_0));
        }

        [Fact]
        public void ModDependencyParsing_ShouldWork()
        {
            var result = ModVersion.TryParseModDependency("SomeMod@1.5.0", out var modName, out var version);

            Assert.True(result);
            Assert.Equal("SomeMod", modName);
            Assert.NotNull(version);
            Assert.Equal(1, version.Major);
        }
    }

    public class ModSchemaTests
    {
        [Fact]
        public void DataSchema_ShouldValidateCorrectData()
        {
            var schema = new ModSchema("TestSchema", ModType.Data);
            schema.Fields["type"] = new FieldSchema("type", FieldType.String) { IsRequired = true };
            schema.Fields["value"] = new FieldSchema("value", FieldType.Number) { IsRequired = true };

            var data = JObject.Parse(@"{ ""type"": ""weapon"", ""value"": 10 }");

            var isValid = schema.Validate(data, out var errors);

            Assert.True(isValid);
            Assert.Empty(errors);
        }

        [Fact]
        public void SchemaValidation_ShouldRejectMissingRequired()
        {
            var schema = new ModSchema("TestSchema", ModType.Data);
            schema.Fields["required_field"] = new FieldSchema("required_field", FieldType.String) { IsRequired = true };

            var data = JObject.Parse(@"{ }");

            var isValid = schema.Validate(data, out var errors);

            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("required_field"));
        }

        [Fact]
        public void StringField_ShouldEnforceMaxLength()
        {
            var schema = new ModSchema("TestSchema", ModType.Data);
            var field = new FieldSchema("name", FieldType.String) { MaxLength = 10 };

            var tooLong = JToken.Parse(@"""this is a very long string""");
            var isValid = field.Validate(tooLong, out var errors);

            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("max length"));
        }

        [Fact]
        public void NumberField_ShouldEnforceBounds()
        {
            var field = new FieldSchema("health", FieldType.Number)
            {
                NumericBounds = (0, 100)
            };

            var outOfBounds = JToken.Parse("150");
            var isValid = field.Validate(outOfBounds, out var errors);

            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("bounds"));
        }

        [Fact]
        public void BehaviorGraphValidation_ShouldCheckNodes()
        {
            var schema = new ModSchema("BehaviorGraph", ModType.BehaviorGraph)
            {
                MaxNodeCount = 10
            };

            var graph = JObject.Parse(@"{
                ""nodes"": [
                    { ""id"": ""node1"" },
                    { ""id"": ""node2"" }
                ],
                ""edges"": [
                    { ""from"": ""node1"", ""to"": ""node2"" }
                ]
            }");

            var isValid = schema.ValidateBehaviorGraph(graph, out var errors);

            Assert.True(isValid);
            Assert.Empty(errors);
        }

        [Fact]
        public void BehaviorGraphValidation_ShouldRejectInvalidEdges()
        {
            var schema = new ModSchema("BehaviorGraph", ModType.BehaviorGraph);

            var graph = JObject.Parse(@"{
                ""nodes"": [
                    { ""id"": ""node1"" }
                ],
                ""edges"": [
                    { ""from"": ""node1"", ""to"": ""nonexistent"" }
                ]
            }");

            var isValid = schema.ValidateBehaviorGraph(graph, out var errors);

            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("nonexistent"));
        }

        [Fact]
        public void BehaviorGraphValidation_ShouldEnforceNodeLimit()
        {
            var schema = new ModSchema("BehaviorGraph", ModType.BehaviorGraph)
            {
                MaxNodeCount = 2
            };

            var graph = JObject.Parse(@"{
                ""nodes"": [
                    { ""id"": ""node1"" },
                    { ""id"": ""node2"" },
                    { ""id"": ""node3"" }
                ],
                ""edges"": []
            }");

            var isValid = schema.ValidateBehaviorGraph(graph, out var errors);

            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("exceeds maximum node count"));
        }
    }

    public class ExecutionGuardsTests
    {
        [Fact]
        public void DisableMod_ShouldTrackDisabledMods()
        {
            var guards = new ExecutionGuards();
            guards.DisableMod("mod.zip", "Test error");

            Assert.True(guards.IsModDisabled("mod.zip"));
        }

        [Fact]
        public void DisabledModsList_ShouldReturnAllDisabled()
        {
            var guards = new ExecutionGuards();
            guards.DisableMod("mod1.zip", "Error 1");
            guards.DisableMod("mod2.zip", "Error 2");

            var disabled = guards.GetDisabledMods().ToList();

            Assert.Equal(2, disabled.Count);
        }

        [Fact]
        public void ValidateFilePath_ShouldAllowSandboxAccess()
        {
            var guards = new ExecutionGuards();
            var sandbox = Path.Combine(Path.GetTempPath(), "mods");

            var result = guards.ValidateFilePath(
                Path.Combine(sandbox, "data.json"),
                sandbox);

            Assert.True(result);
        }

        [Fact]
        public void ValidateFilePath_ShouldBlockEscapeSandbox()
        {
            var guards = new ExecutionGuards();
            var sandbox = Path.Combine(Path.GetTempPath(), "mods");

            var result = guards.ValidateFilePath(
                Path.Combine(Path.GetTempPath(), "../../etc/passwd"),
                sandbox);

            Assert.False(result);
        }

        [Fact]
        public void ValidateParameterBounds_ShouldPassInRange()
        {
            var guards = new ExecutionGuards();

            var result = guards.ValidateParameterBounds(50, 0, 100);

            Assert.True(result);
        }

        [Fact]
        public void ValidateParameterBounds_ShouldFailOutOfRange()
        {
            var guards = new ExecutionGuards();

            var result = guards.ValidateParameterBounds(150, 0, 100);

            Assert.False(result);
        }

        [Fact]
        public void ExecutionContext_ShouldTrackElapsedTime()
        {
            var guards = new ExecutionGuards();
            var context = guards.CreateExecutionContext("TestMod");

            System.Threading.Thread.Sleep(10);
            context.Complete();

            Assert.True(context.ElapsedTime.TotalMilliseconds >= 10);
        }

        [Fact]
        public void ExecutionContext_ShouldDetectTimeout()
        {
            var guards = new ExecutionGuards();
            var context = guards.CreateExecutionContext("TestMod", 10);

            System.Threading.Thread.Sleep(50);

            Assert.True(context.IsTimedOut);
        }
    }

    public class ModSurfaceDeclarationTests
    {
        [Fact]
        public void DeclareSurface_ShouldAddToList()
        {
            var declaration = new PublicAPI.ModSurfaceDeclaration("TestGame", "1.0.0");
            declaration.DeclareDataSurface("GameBalance", "Balance changes", "data/balance.json");

            Assert.Single(declaration.Surfaces);
            Assert.Equal("GameBalance", declaration.Surfaces[0].Name);
        }

        [Fact]
        public void GenerateSurfaceSummary_ShouldCreateMarkdown()
        {
            var declaration = new PublicAPI.ModSurfaceDeclaration("TestGame", "1.0.0");
            declaration.DeclareDataSurface("Config", "Configuration", "data/config.json");

            var summary = declaration.GenerateSurfaceSummary();

            Assert.Contains("TestGame", summary);
            Assert.Contains("Config", summary);
        }

        [Fact]
        public void ExportAsJson_ShouldSerialize()
        {
            var declaration = new PublicAPI.ModSurfaceDeclaration("TestGame", "1.0.0");
            declaration.DeclareDataSurface("Data", "Data mod", "data/data.json");

            var json = declaration.ExportAsJson();

            Assert.NotEmpty(json);
            Assert.Contains("TestGame", json);
        }
    }
}
