using System;
using System.Collections.Generic;
using System.Linq;
using RealTPS.Core.Cfg;
using RealTPS.Core.Hosts;
using RealTPS.Core.Materials;
using RealTPS.Core.Physics;
using Xunit;

namespace RealTPS.Core.Tests
{
    public class MaterialParserTests
    {
        private static MaterialDefinition TryParse(string cfg, out List<ValidationIssue> issues)
        {
            issues = new List<ValidationIssue>();
            return MaterialParser.Parse(CfgParser.Parse(cfg).Nodes[0], issues);
        }

        [Fact]
        public void FixturesParse()
        {
            Assert.Equal("test-ablator", TestMaterials.Ablator().Id);
            Assert.True(TestMaterials.Ablator().HasConsumable);
            Assert.Equal(25.0, TestMaterials.Ablator().ConsumableArealDensity, 9);
            Assert.False(TestMaterials.Tile().HasConsumable);
            Assert.Equal("RTPS_test_tile", TestMaterials.Tile().UpgradeId);
            Assert.Equal(string.Empty, TestMaterials.Ablator().UpgradeId);
            Assert.Equal("RtpsTestCarbonCloth", TestMaterials.Carbon().ConsumableResource);
        }

        [Theory]
        [InlineData("id = test-ablator", "id = Test Ablator")]
        [InlineData("schemaVersion = 1", "schemaVersion = 2")]
        [InlineData("schemaVersion = 1", "schemaVersion = 0")]
        [InlineData("schemaVersion = 1", "schemaVersion = 1.5")]
        [InlineData("schemaVersion = 1", "schemaVersion = latest")]
        [InlineData("schemaVersion = 1", "schemaVersion = ")]
        [InlineData("costPerKg = 2", "costPerKg = 2\n    stockNode = landing")]
        [InlineData("costPerKg = 2", "costPerKg = 2\n    techNode = two nodes")]
        [InlineData("costPerKg = 2", "costPerKg = 2\n    SOMETHING { a = 1 }")]
        [InlineData("heatOfAblation = 5.0e6", "heatOfAblation = 5.0e6\n        charYield = 0.3")]
        [InlineData("density = 500", "density = NaN")]
        [InlineData("density = 500", "density = -4")]
        [InlineData("density = 500", "")]
        [InlineData("emissivity = 0.7", "emissivity = 1.4")]
        [InlineData("permittedHostClasses = rigid_disc", "permittedHostClasses = *")]
        [InlineData("permittedHostClasses = rigid_disc", "permittedHostClasses = ")]
        [InlineData("family = rigid_ablative", "family = magic")]
        [InlineData("family = rigid_ablative", "family = rigid_ablative\n    speculative = maybe")]
        [InlineData("family = rigid_ablative", "family = rigid_ablative\n    speculative = true")]
        [InlineData("family = rigid_ablative", "family = rigid_ablative\n    realismTier = historical")]
        [InlineData("family = rigid_ablative", "family = rigid_ablative\n    availabilityStatus = ASSIGNED")]
        [InlineData("heatOfAblation = 5.0e6", "heatOfAblation = Infinity")]
        [InlineData("heatOfAblation = 5.0e6", "heatOfAblation = 0")]
        [InlineData("onsetTemp = 600", "onsetTemp = 2600")]
        [InlineData("resource = RtpsTestAblator", "resource = bad name!")]
        [InlineData("shearRate = 0.02", "shearRate = 0")]
        [InlineData("conductivity = 0.2", "conductivity = 0")]
        [InlineData("conductivity = 0.2", "conductivty = 0.2")]
        [InlineData("bondLimitTemp = 520", "bondLimitTemp = 50")]
        [InlineData("bondLimitTemp = 520", "bondLimitTemp = 520\n    damageSurfaceTemp = 2500")]
        [InlineData("bondLimitTemp = 520", "bondLimitTemp = 520\n    skinInternalConductionMult = 0.5")]
        [InlineData("bondLimitTemp = 520", "bondLimitTemp = 520\n    maxStructureTemp = 700")]
        [InlineData("bondLimitTemp = 520", "bondLimitTemp = 520\n    depletedMaxSurfaceTemp = 1000")]
        [InlineData("costPerKg = 2", "costPerKg = 2\n    costPerKg = 3")]
        public void InvalidProfilesAreRejectedWithoutDefaults(string find, string replace)
        {
            Assert.Contains(find, TestMaterials.AblatorCfg);
            List<ValidationIssue> issues;
            MaterialDefinition m = TryParse(TestMaterials.AblatorCfg.Replace(find, replace), out issues);
            Assert.Null(m);
            Assert.Contains(issues, i => i.IsError);
        }

        [Fact]
        public void SchemaVersionIsOptionalAndMeansTheInstalledSchema()
        {
            // Stated: read as exactly that version.
            Assert.Contains("schemaVersion = 1", TestMaterials.AblatorCfg);
            Assert.Equal(1, TestMaterials.Ablator().SchemaVersion);

            // Omitted: the schema of the installed build.
            Assert.DoesNotContain("schemaVersion", TestMaterials.CarbonCfg);
            Assert.Equal(MaterialDefinition.CurrentSchemaVersion, TestMaterials.Carbon().SchemaVersion);
            Assert.Equal(MaterialDefinition.CurrentSchemaVersion, TestMaterials.Parse(TestMaterials.AblatorCfg.Replace("schemaVersion = 1", string.Empty)).SchemaVersion);

            // A snapshot always records the version it was written with.
            Assert.Contains("schemaVersion = " + MaterialDefinition.CurrentSchemaVersion, CfgParser.Write(MaterialWriter.Write(TestMaterials.Carbon())));
            Assert.Equal(1, MaterialDefinition.CurrentSchemaVersion);
        }

        [Fact]
        public void OneTechNodeDecidesTheUnlock()
        {
            Assert.Equal("hypersonicFlight", TestMaterials.Tile().TechNode);
            Assert.Equal("RTPS_test_tile", TestMaterials.Tile().UpgradeId);
            Assert.Equal(string.Empty, TestMaterials.Ablator().TechNode);
            Assert.Contains("techNode = hypersonicFlight", CfgParser.Write(MaterialWriter.Write(TestMaterials.Tile())));
        }

        [Fact]
        public void SpeculativeIsOptionalAndSurvivesASnapshot()
        {
            Assert.False(TestMaterials.Tile().Speculative);
            Assert.DoesNotContain("speculative", CfgParser.Write(MaterialWriter.Write(TestMaterials.Tile())));

            // The tile has a techNode, so it may be speculative (a speculative material is never a baseline).
            MaterialDefinition m = TestMaterials.Parse(TestMaterials.TileCfg.Replace("family = rigid_reusable", "family = rigid_reusable\n    speculative = true"));
            Assert.True(m.Speculative);

            var issues = new List<ValidationIssue>();
            MaterialDefinition back;
            Assert.Equal(MaterialParser.SnapshotStatus.Ok, MaterialParser.ParseSnapshot(MaterialWriter.Write(m), issues, out back));
            Assert.True(back.Speculative);
        }

        [Fact]
        public void SnapshotsWithTheRemovedKeysStillLoad()
        {
            // Saves written before realismTier and availabilityStatus were removed still carry them.
            CfgNode old = MaterialWriter.Write(TestMaterials.Ablator());
            old.AddValue("realismTier", "historical");
            old.AddValue("availabilityStatus", "ASSIGNED");
            var issues = new List<ValidationIssue>();
            MaterialDefinition back;
            Assert.Equal(MaterialParser.SnapshotStatus.Ok, MaterialParser.ParseSnapshot(old, issues, out back));
            Assert.DoesNotContain(issues, i => i.IsError);
            Assert.False(back.Speculative);
        }

        [Fact]
        public void NonAblativeFamiliesMayNotDeclareACoolingSink()
        {
            string cfg = TestMaterials.TileCfg.Replace("techNode = hypersonicFlight", "techNode = hypersonicFlight\n    ABLATION { resource = RtpsX\n onsetTemp = 600\n activationTemp = 8000\n rateCoefficient = 1\n heatOfAblation = 1e6 }");
            List<ValidationIssue> issues;
            Assert.Null(TryParse(cfg, out issues));
            Assert.Contains(issues, i => i.Message.Contains("only rigid_ablative"));
        }

        [Fact]
        public void RigidAblativeNeedsAnAblationNode()
        {
            string cfg = TestMaterials.TileCfg.Replace("family = rigid_reusable", "family = rigid_ablative");
            List<ValidationIssue> issues;
            Assert.Null(TryParse(cfg, out issues));
        }

        [Fact]
        public void EveryCatalogueProfileStatesConductivityAndBondLimit()
        {
            // The layer model and the damage model need both; a catalogue profile without them is refused.
            foreach (string key in new[] { "conductivity", "bondLimitTemp" })
            {
                string cfg = System.Text.RegularExpressions.Regex.Replace(TestMaterials.TileCfg, key + " = [0-9.]+\\s*", string.Empty);
                Assert.DoesNotContain(key, cfg);
                List<ValidationIssue> issues;
                Assert.Null(TryParse(cfg, out issues));
            }
            Assert.True(TestMaterials.Tile().HasLayerModel);
            Assert.Equal(1600.0, TestMaterials.Tile().DamageSurfaceTemp);
            Assert.Equal(0.0, TestMaterials.Ablator().DamageSurfaceTemp);
        }

        [Fact]
        public void SnapshotRoundTripsEveryField()
        {
            foreach (MaterialDefinition original in new[] { TestMaterials.Ablator(), TestMaterials.Carbon(), TestMaterials.Tile() })
            {
                string text = CfgParser.Write(MaterialWriter.Write(original));
                var issues = new List<ValidationIssue>();
                MaterialDefinition copy = MaterialParser.Parse(CfgParser.Parse(text).Nodes[0], issues);
                Assert.NotNull(copy);
                Assert.Equal(CfgParser.Write(MaterialWriter.Write(original)), CfgParser.Write(MaterialWriter.Write(copy)));
                Assert.Equal(original.Id, copy.Id);
                Assert.Equal(original.Family, copy.Family);
                Assert.Equal(original.MaxSurfaceTemp, copy.MaxSurfaceTemp);
                Assert.Equal(original.ConsumableResource, copy.ConsumableResource);
                Assert.Equal(original.PermittedHostClasses, copy.PermittedHostClasses);
            }
        }

        [Fact]
        public void CatalogueRejectsDuplicateIdsAndSharedResources()
        {
            CfgNode a = CfgParser.Parse(TestMaterials.AblatorCfg).Nodes[0];
            CfgNode duplicate = CfgParser.Parse(TestMaterials.AblatorCfg).Nodes[0];
            CfgNode sharing = CfgParser.Parse(TestMaterials.AblatorCfg.Replace("id = test-ablator", "id = other-ablator")).Nodes[0];
            MaterialCatalogue catalogue = MaterialCatalogue.Load(new[] { a, duplicate, sharing });
            Assert.Single(catalogue.Materials);
            Assert.Equal(2, catalogue.Issues.Count(i => i.IsError));
            MaterialDefinition found;
            Assert.True(catalogue.TryGet("test-ablator", out found));
            Assert.False(catalogue.TryGet("other-ablator", out found));
            Assert.False(catalogue.TryGet(null, out found));
        }
    }

    public class CfgParserTests
    {
        [Fact]
        public void ParsesNodesValuesCommentsAndModuleManagerNames()
        {
            CfgNode root = CfgParser.Parse("// c\n@PART[HeatShield1]:NEEDS[RealTPSRuntime&B9PartSwitch]:FOR[RealTPS]\n{\n\t!MODULE[ModuleAblator] {}\n\t@cost -= 100 // note\n\tMODULE { name = X\n NESTED\n {\n  a = b = c\n }\n }\n}\n");
            CfgNode part = root.Nodes[0];
            Assert.Equal("@PART[HeatShield1]:NEEDS[RealTPSRuntime&B9PartSwitch]:FOR[RealTPS]", part.Name);
            Assert.Equal("!MODULE[ModuleAblator]", part.Nodes[0].Name);
            // ModuleManager operators stay part of the key: "@cost -= 100" is key "@cost -", value "100".
            Assert.Equal("100", part.GetValue("@cost -"));
            Assert.Equal("b = c", part.GetNode("MODULE").GetNode("NESTED").GetValue("a"));
        }

        [Theory]
        [InlineData("A {")]
        [InlineData("A { } }")]
        [InlineData("{ }")]
        [InlineData("A\nB { }")]
        public void MalformedTextThrows(string text)
        {
            Assert.Throws<CfgParser.ParseException>(() => CfgParser.Parse(text));
        }
    }

    public class EmissivityAndLegacyTests
    {
        [Fact]
        public void ShearErosionDoesNotCreateCharEmissivity()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            double capacity = 30.0;
            double remaining = capacity;
            double pyrolysed = 0.0;
            for (int i = 0; i < 20000 && remaining > 0.0; i++)
            {
                var input = new StepInput { Dt = 0.02, SkinTemp = 450.0, SkinHeatCapacity = 2.0e5, Area = 1.2, RemainingMass = remaining, DynamicPressure = 400000.0 };
                StepResult r = AblationSolver.Step(m, input);
                remaining -= r.ConsumedMass;
                pyrolysed += r.PyrolysedMass;
            }
            Assert.True(remaining < capacity * 0.5, "shear should have stripped material");
            Assert.Equal(0.0, pyrolysed);
            Assert.Equal(m.Emissivity, AblationSolver.SurfaceEmissivity(m, pyrolysed / capacity), 12);
        }

        [Fact]
        public void LegacyProfileReproducesStockRateAndSink()
        {
            double area = 1.27;
            double capacity = 200.0;
            MaterialDefinition legacy = LegacyProfile.Create(area, capacity, 0.4, 3300.0);
            double skin = 1900.0;
            var input = new StepInput { Dt = 0.02, SkinTemp = skin, SkinHeatCapacity = 1.0e9, Area = area, RemainingMass = 150.0 };
            StepResult r = AblationSolver.Step(legacy, input);

            double stockUnitsPerSecond = 0.1 * Math.Exp(-7500.0 / skin) * capacity;
            Assert.Equal(stockUnitsPerSecond * 0.02, r.PyrolysedMass, 10);
            Assert.Equal(stockUnitsPerSecond * 0.02 * 2.4e6, r.HeatAbsorbed, 4);
            Assert.Equal(0.4, AblationSolver.SurfaceEmissivity(legacy, 0.9), 12);
            // No conductivity and no bond limit: the legacy profile has no layer model and takes no damage.
            Assert.False(legacy.HasLayerModel);
            Assert.Equal(0.0, legacy.BondLimitTemp);
            Assert.Equal(3300.0, legacy.MaxSurfaceTemp);
            Assert.Equal("Ablator", legacy.ConsumableResource);

            input.SkinTemp = 500.0;
            Assert.Equal(0.0, AblationSolver.Step(legacy, input).ConsumedMass);
            Assert.Throws<ArgumentException>(() => LegacyProfile.Create(0.0, 200.0, 0.4, 3300.0));
            Assert.Throws<ArgumentException>(() => LegacyProfile.Create(1.0, double.NaN, 0.4, 3300.0));
        }
    }

    public class InventoryTransferTests
    {
        [Theory]
        [InlineData(200.0, 200.0, 36.6, 6.0e6, 36.6)]   // full legacy shield -> full, capped by capacity
        [InlineData(100.0, 200.0, 36.6, 6.0e6, 18.3)]   // half full stays half full
        [InlineData(10.0, 200.0, 36.6, 6.0e6, 1.83)]    // fraction is the binding limit
        [InlineData(10.0, 200.0, 36.6, 30.0e6, 0.8)]    // energy equivalence is the binding limit
        [InlineData(0.0, 200.0, 36.6, 6.0e6, 0.0)]      // empty stays empty
        [InlineData(500.0, 200.0, 36.6, 6.0e6, 36.6)]   // over-full legacy cannot exceed capacity
        public void LegacyConversionNeverAddsProtection(double amount, double capacity, double newCapacity, double newH, double expected)
        {
            double result = InventoryTransfer.ConvertLegacy(amount, capacity, InventoryTransfer.StockHeatOfAblation, newCapacity, newH);
            Assert.Equal(expected, result, 9);
            Assert.True(result <= newCapacity);
            Assert.True(result * newH <= Math.Max(amount, 0.0) * InventoryTransfer.StockHeatOfAblation + 1e-6);
            Assert.True(result / newCapacity <= Math.Min(1.0, amount / capacity) + 1e-12);
        }

        [Theory]
        [InlineData(double.NaN, 200.0, 36.6)]
        [InlineData(100.0, 0.0, 36.6)]
        [InlineData(100.0, 200.0, double.PositiveInfinity)]
        [InlineData(-5.0, 200.0, 36.6)]
        public void LegacyConversionRejectsInvalidInput(double amount, double capacity, double newCapacity)
        {
            Assert.Equal(0.0, InventoryTransfer.ConvertLegacy(amount, capacity, 2.4e6, newCapacity, 6.0e6));
        }

        [Fact]
        public void LegacyConversionWithoutSinkUsesFractionOnly()
        {
            Assert.Equal(5.0, InventoryTransfer.ConvertLegacy(100.0, 200.0, 2.4e6, 10.0, 0.0), 12);
        }

        [Theory]
        [InlineData(0.5, 40.0, 40.0, 20.0)]
        [InlineData(0.5, 40.0, 5.0, 5.0)]     // never more than is present
        [InlineData(1.7, 40.0, 100.0, 40.0)]
        [InlineData(-0.2, 40.0, 40.0, 0.0)]
        [InlineData(double.NaN, 40.0, 40.0, 0.0)]
        [InlineData(0.5, 0.0, 40.0, 0.0)]
        public void CarriedFractionNeverExceedsWhatIsPresent(double fraction, double capacity, double current, double expected)
        {
            Assert.Equal(expected, InventoryTransfer.CarryFraction(fraction, capacity, current), 12);
        }
    }

    public class HostTests
    {
        private const string HostCfg = @"
RTPS_HOST
{
    part = TestShield
    hostClass = rigid_disc
    partRole = dedicated
    area = 1.2
    substrateMaxTemp = 1100
    materials = test-ablator, test-tile
}";

        private static MaterialCatalogue Catalogue()
        {
            return MaterialCatalogue.Load(new[]
            {
                CfgParser.Parse(TestMaterials.AblatorCfg).Nodes[0],
                CfgParser.Parse(TestMaterials.TileCfg).Nodes[0],
                CfgParser.Parse(TestMaterials.CarbonCfg).Nodes[0]
            });
        }

        private static HostDefinition ParseHost(string cfg, List<ValidationIssue> issues)
        {
            return HostDefinition.Parse(CfgParser.Parse(cfg).Nodes[0], issues);
        }

        private static string Host(string part, string needs)
        {
            return "RTPS_HOST\n{\n part = " + part + (needs.Length > 0 ? "\n needs = " + needs : string.Empty)
                + "\n hostClass = rigid_disc\n partRole = dedicated\n area = 1\n substrateMaxTemp = 1100\n materials = test-ablator\n}";
        }

        private static string UnlockOf(params HostDefinition[] hosts)
        {
            MaterialCatalogue catalogue = MaterialCatalogue.Load(CfgParser.Parse(
                TestMaterials.AblatorCfg.Replace("costPerKg = 2", "costPerKg = 2\n    techNode = landing\n    unlockCost = 100")).GetNodes(MaterialParser.NodeName));
            Assert.DoesNotContain(catalogue.Issues, i => i.IsError);
            return RealTPS.Core.Generation.PatchGenerator.Upgrades(catalogue, hosts);
        }

        [Fact]
        public void AnUnlockShowsAnAlwaysInstalledPartAndNeedsNoOtherMod()
        {
            // The other mod's host comes first in file order (data/hosts is read alphabetically).
            var issues = new List<ValidationIssue>();
            string text = UnlockOf(ParseHost(Host("ModShield", "OtherMod"), issues), ParseHost(Host("StockShield", ""), issues));
            Assert.Contains("partIcon = StockShield", text);
            Assert.Contains("PARTUPGRADE:NEEDS[RealTPSRuntime&B9PartSwitch]", text);
        }

        [Fact]
        public void AnUnlockOfferedOnlyByOtherModsNeedsOneOfThem()
        {
            var issues = new List<ValidationIssue>();
            string text = UnlockOf(ParseHost(Host("ModShield", "OtherMod"), issues), ParseHost(Host("ThirdShield", "ThirdMod"), issues),
                ParseHost(Host("ModShield2", "OtherMod"), issues));
            Assert.Contains("partIcon = ModShield", text);
            Assert.Contains("PARTUPGRADE:NEEDS[RealTPSRuntime&B9PartSwitch&OtherMod|ThirdMod]", text);
        }

        [Fact]
        public void ValidHostPassesAndComputesMasses()
        {
            var issues = new List<ValidationIssue>();
            HostDefinition host = ParseHost(HostCfg, issues);
            Assert.NotNull(host);
            Compatibility.ValidateHost(host, Catalogue(), issues);
            Assert.DoesNotContain(issues, i => i.IsError);
            Assert.Equal(30.0, host.ConsumableMass(TestMaterials.Ablator()), 9);
            Assert.Equal(0.0, host.ConsumableMass(TestMaterials.Tile()), 9);
            Assert.Equal(10.8, host.DryMass(TestMaterials.Tile()), 9);
            Assert.Equal(1080.0, host.DryCost(TestMaterials.Tile()), 9);
        }

        [Theory]
        [InlineData("materials = test-ablator, test-tile", "materials = ")]
        [InlineData("materials = test-ablator, test-tile", "materials = test-ablator, test-ablator")]
        [InlineData("part = TestShield", "part = *")]
        [InlineData("area = 1.2", "area = 0")]
        [InlineData("substrateMaxTemp = 1100", "")]
        public void BrokenHostsAreRejected(string find, string replace)
        {
            var issues = new List<ValidationIssue>();
            Assert.Null(ParseHost(HostCfg.Replace(find, replace), issues));
            Assert.Contains(issues, i => i.IsError);
        }

        [Fact]
        public void IncompatibleUnknownAndLockedBaselineAssignmentsFail()
        {
            MaterialCatalogue catalogue = Catalogue();

            var issues = new List<ValidationIssue>();
            Compatibility.ValidateHost(ParseHost(HostCfg.Replace("test-ablator, test-tile", "test-ablator, test-carbon-fabric"), issues), catalogue, issues);
            Assert.Contains(issues, i => i.IsError && i.Message.Contains("does not permit host class"));

            issues = new List<ValidationIssue>();
            Compatibility.ValidateHost(ParseHost(HostCfg.Replace("test-ablator, test-tile", "test-ablator, nope"), issues), catalogue, issues);
            Assert.Contains(issues, i => i.IsError && i.Message.Contains("not a valid catalogue entry"));

            issues = new List<ValidationIssue>();
            Compatibility.ValidateHost(ParseHost(HostCfg.Replace("test-ablator, test-tile", "test-tile, test-ablator"), issues), catalogue, issues);
            Assert.Contains(issues, i => i.IsError && i.Message.Contains("must not require an unlock"));
        }

        [Fact]
        public void CapabilitiesAreRequiredNotAssumed()
        {
            MaterialDefinition carbon = TestMaterials.Carbon();
            Assert.NotNull(Compatibility.Check(carbon, "deployable_rib_fabric", new List<string>()));
            Assert.NotNull(Compatibility.Check(carbon, "deployable_rib_fabric", null));
            Assert.Null(Compatibility.Check(carbon, "deployable_rib_fabric", new List<string> { "hot_structure" }));
            Assert.NotNull(Compatibility.Check(carbon, "rigid_disc", new List<string> { "hot_structure" }));
            Assert.NotNull(Compatibility.Check(carbon, "", new List<string> { "hot_structure" }));
            Assert.NotNull(Compatibility.Check(null, "rigid_disc", null));
        }
    }
}
