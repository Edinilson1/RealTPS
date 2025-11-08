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
        public void RigidAblativeNeedsAnAblationNode()
        {
            string cfg = TestMaterials.TileCfg.Replace("family = rigid_reusable", "family = rigid_ablative");
            List<ValidationIssue> issues;
            Assert.Null(TryParse(cfg, out issues));
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
}
