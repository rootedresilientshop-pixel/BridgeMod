// =============================================================================
// ModSurfaceTests.cs — BridgeMod.SDK Phase 2 — Developer Mod Surfaces
// =============================================================================
// Tests for Phase 2 mod surface metadata types.
// Validates: registration safety, duplicate prevention, null guards,
// empty-name guards, summary grouping, alphabetical ordering.
//
// These tests exercise only metadata behavior — no runtime logic is tested here.
// All Phase 1 tests continue to pass unmodified.
// =============================================================================

using System;
using BridgeMod.Bridge;
using Xunit;

namespace BridgeMod.Tests
{
    public class ModSurfaceDeclarationTests
    {
        [Fact]
        public void Declaration_WithValidArgs_CreatesSuccessfully()
        {
            var decl = new ModSurfaceDeclaration(
                "WeaponBalance",
                ModSurfaceCategory.Data,
                ModSurfaceStatus.Enabled,
                "Allows modding of weapon stat balance tables.");

            Assert.Equal("WeaponBalance", decl.Name);
            Assert.Equal(ModSurfaceCategory.Data, decl.Category);
            Assert.Equal(ModSurfaceStatus.Enabled, decl.Status);
            Assert.Equal("Allows modding of weapon stat balance tables.", decl.Description);
        }

        [Fact]
        public void Declaration_EmptyName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new ModSurfaceDeclaration(
                    "",
                    ModSurfaceCategory.Data,
                    ModSurfaceStatus.Enabled,
                    "Some description."));
        }

        [Fact]
        public void Declaration_WhitespaceName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new ModSurfaceDeclaration(
                    "   ",
                    ModSurfaceCategory.Data,
                    ModSurfaceStatus.Enabled,
                    "Some description."));
        }

        [Fact]
        public void Declaration_NullName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new ModSurfaceDeclaration(
                    null!,
                    ModSurfaceCategory.Data,
                    ModSurfaceStatus.Enabled,
                    "Some description."));
        }

        [Fact]
        public void Declaration_EmptyDescription_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new ModSurfaceDeclaration(
                    "ValidName",
                    ModSurfaceCategory.Data,
                    ModSurfaceStatus.Enabled,
                    ""));
        }
    }

    public class ModSurfaceRegistryTests
    {
        private static ModSurfaceDeclaration MakeDeclaration(
            string name,
            ModSurfaceCategory category = ModSurfaceCategory.Data,
            ModSurfaceStatus status = ModSurfaceStatus.Enabled) =>
            new ModSurfaceDeclaration(name, category, status, $"Description for {name}.");

        [Fact]
        public void Registry_Register_AddsDeclaration()
        {
            var registry = new ModSurfaceRegistry();
            registry.Register(MakeDeclaration("Armor"));

            Assert.Single(registry.Surfaces);
            Assert.Equal("Armor", registry.Surfaces[0].Name);
        }

        [Fact]
        public void Registry_Register_NullDeclaration_ThrowsArgumentNullException()
        {
            var registry = new ModSurfaceRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
        }

        [Fact]
        public void Registry_Register_DuplicateName_ThrowsInvalidOperationException()
        {
            var registry = new ModSurfaceRegistry();
            registry.Register(MakeDeclaration("WeaponBalance"));

            Assert.Throws<InvalidOperationException>(() =>
                registry.Register(MakeDeclaration("WeaponBalance")));
        }

        [Fact]
        public void Registry_DuplicateCheck_IsCaseSensitive()
        {
            var registry = new ModSurfaceRegistry();
            registry.Register(MakeDeclaration("Armor"));

            // Different case — should NOT throw; case-sensitive check
            var exception = Record.Exception(() => registry.Register(MakeDeclaration("armor")));
            Assert.Null(exception);
            Assert.Equal(2, registry.Surfaces.Count);
        }

        [Fact]
        public void Registry_Surfaces_IsReadOnly()
        {
            var registry = new ModSurfaceRegistry();
            registry.Register(MakeDeclaration("Items"));

            // IReadOnlyList — verify it is not castable to a mutable list
            Assert.IsNotType<System.Collections.Generic.List<ModSurfaceDeclaration>>(registry.Surfaces);
        }

        [Fact]
        public void Registry_MultipleRegistrations_PreservesOrder()
        {
            var registry = new ModSurfaceRegistry();
            registry.Register(MakeDeclaration("Zebra"));
            registry.Register(MakeDeclaration("Apple"));
            registry.Register(MakeDeclaration("Mango"));

            // Registry preserves insertion order
            Assert.Equal("Zebra", registry.Surfaces[0].Name);
            Assert.Equal("Apple", registry.Surfaces[1].Name);
            Assert.Equal("Mango", registry.Surfaces[2].Name);
        }
    }

    public class ModSurfaceSummaryGeneratorTests
    {
        private static ModSurfaceRegistry BuildRegistry(
            params (string name, ModSurfaceCategory cat, ModSurfaceStatus status)[] surfaces)
        {
            var registry = new ModSurfaceRegistry();
            foreach (var (name, cat, status) in surfaces)
                registry.Register(new ModSurfaceDeclaration(name, cat, status, $"Description for {name}."));
            return registry;
        }

        [Fact]
        public void Generator_NullRegistry_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ModSurfaceSummaryGenerator.Generate(null!));
        }

        [Fact]
        public void Generator_EmptyRegistry_ReturnsNoSurfacesNotice()
        {
            var registry = new ModSurfaceRegistry();
            string result = ModSurfaceSummaryGenerator.Generate(registry, "TestGame");

            Assert.Contains("No surfaces declared", result);
        }

        [Fact]
        public void Generator_GroupsByCategory()
        {
            var registry = BuildRegistry(
                ("WeaponBalance", ModSurfaceCategory.Data, ModSurfaceStatus.Enabled),
                ("AIBehavior", ModSurfaceCategory.BehaviorGraphs, ModSurfaceStatus.Limited));

            string result = ModSurfaceSummaryGenerator.Generate(registry, "TestGame");

            Assert.Contains("[Data]", result);
            Assert.Contains("[BehaviorGraphs]", result);
        }

        [Fact]
        public void Generator_OrdersSurfacesAlphabeticallyWithinCategory()
        {
            var registry = BuildRegistry(
                ("Zebra", ModSurfaceCategory.Data, ModSurfaceStatus.Enabled),
                ("Apple", ModSurfaceCategory.Data, ModSurfaceStatus.Enabled),
                ("Mango", ModSurfaceCategory.Data, ModSurfaceStatus.Enabled));

            string result = ModSurfaceSummaryGenerator.Generate(registry);

            int applePos = result.IndexOf("Apple", StringComparison.Ordinal);
            int mangoPos = result.IndexOf("Mango", StringComparison.Ordinal);
            int zebraPos = result.IndexOf("Zebra", StringComparison.Ordinal);

            Assert.True(applePos < mangoPos, "Apple should appear before Mango");
            Assert.True(mangoPos < zebraPos, "Mango should appear before Zebra");
        }

        [Fact]
        public void Generator_OrdersCategoriesAlphabetically()
        {
            var registry = BuildRegistry(
                ("ProceduralItem", ModSurfaceCategory.ProceduralInputs, ModSurfaceStatus.Enabled),
                ("DataItem", ModSurfaceCategory.Data, ModSurfaceStatus.Enabled),
                ("BehaviorItem", ModSurfaceCategory.BehaviorGraphs, ModSurfaceStatus.Enabled));

            string result = ModSurfaceSummaryGenerator.Generate(registry);

            // BehaviorGraphs < Data < ProceduralInputs alphabetically
            int behaviorPos = result.IndexOf("[BehaviorGraphs]", StringComparison.Ordinal);
            int dataPos = result.IndexOf("[Data]", StringComparison.Ordinal);
            int proceduralPos = result.IndexOf("[ProceduralInputs]", StringComparison.Ordinal);

            Assert.True(behaviorPos < dataPos, "BehaviorGraphs should appear before Data");
            Assert.True(dataPos < proceduralPos, "Data should appear before ProceduralInputs");
        }

        [Fact]
        public void Generator_IncludesStatusInOutput()
        {
            var registry = BuildRegistry(
                ("Items", ModSurfaceCategory.Data, ModSurfaceStatus.Disabled));

            string result = ModSurfaceSummaryGenerator.Generate(registry);

            Assert.Contains("[Disabled]", result);
        }

        [Fact]
        public void Generator_IncludesTitleInHeader()
        {
            var registry = BuildRegistry(
                ("Items", ModSurfaceCategory.Data, ModSurfaceStatus.Enabled));

            string result = ModSurfaceSummaryGenerator.Generate(registry, "MyAwesomeGame");

            Assert.Contains("MyAwesomeGame", result);
        }

        [Fact]
        public void Generator_DefaultsToGameWhenNoTitle()
        {
            var registry = BuildRegistry(
                ("Items", ModSurfaceCategory.Data, ModSurfaceStatus.Enabled));

            string result = ModSurfaceSummaryGenerator.Generate(registry);

            Assert.Contains("Game", result);
        }
    }
}
