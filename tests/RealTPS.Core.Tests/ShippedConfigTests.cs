using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealTPS.Core.Cfg;
using RealTPS.Core.Generation;
using RealTPS.Core.Hosts;
using RealTPS.Core.Materials;
using Xunit;

namespace RealTPS.Core.Tests
{
    /// <summary>Checks on the configuration that actually ships, read from the working tree.</summary>
    public class ShippedConfigTests
    {
        private static readonly string Root = FindRoot();
        private static readonly string GameData = Path.Combine(Root, "GameData", "RealTPS");

        private static string FindRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "GameData", "RealTPS")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new InvalidOperationException("repository root not found");
        }

        private static MaterialCatalogue LoadCatalogue()
        {
            var nodes = new List<CfgNode>();
            foreach (string file in Directory.GetFiles(Path.Combine(GameData, "Materials"), "*.cfg").OrderBy(f => f, StringComparer.Ordinal))
            {
                nodes.AddRange(CfgParser.Parse(File.ReadAllText(file)).GetNodes(MaterialParser.NodeName));
            }
            return MaterialCatalogue.Load(nodes);
        }

        private static List<HostDefinition> LoadHosts(MaterialCatalogue catalogue, List<ValidationIssue> issues)
        {
            var hosts = new List<HostDefinition>();
            foreach (string file in Directory.GetFiles(Path.Combine(Root, "data", "hosts"), "*.cfg").OrderBy(f => f, StringComparer.Ordinal))
            {
                foreach (CfgNode node in CfgParser.Parse(File.ReadAllText(file)).GetNodes(HostDefinition.NodeName))
                {
                    HostDefinition host = HostDefinition.Parse(node, issues);
                    Assert.NotNull(host);
                    Compatibility.ValidateHost(host, catalogue, issues);
                    hosts.Add(host);
                }
            }
            return hosts;
        }

        private static string Read(string relative)
        {
            return File.ReadAllText(Path.Combine(Root, relative)).Replace("\r\n", "\n");
        }

        [Fact]
        public void CatalogueAndHostsValidateWithoutIssues()
        {
            MaterialCatalogue catalogue = LoadCatalogue();
            Assert.Empty(catalogue.Issues);
            var issues = new List<ValidationIssue>();
            List<HostDefinition> hosts = LoadHosts(catalogue, issues);
            Assert.Empty(issues);
            Assert.NotEmpty(hosts);
        }

        [Fact]
        public void EveryFamilyHasAtLeastOneProfile()
        {
            MaterialCatalogue catalogue = LoadCatalogue();
            foreach (ConstructionType family in Enum.GetValues(typeof(ConstructionType)))
            {
                Assert.Contains(catalogue.Materials, m => m.Family == family);
            }
        }

        [Fact]
        public void GeneratedFilesAreCurrent()
        {
            MaterialCatalogue catalogue = LoadCatalogue();
            List<HostDefinition> hosts = LoadHosts(catalogue, new List<ValidationIssue>());
            Assert.Equal(PatchGenerator.Resources(catalogue), Read(GeneratedFiles.Resources));
            Assert.Equal(PatchGenerator.Upgrades(catalogue, hosts), Read(GeneratedFiles.Unlocks));
            Assert.Equal(PatchGenerator.Hosts(catalogue, hosts), Read(GeneratedFiles.StockHosts));
        }

        private static List<ValidationIssue> TreeIssues(string fileName, string text)
        {
            var issues = new List<ValidationIssue>();
            TechTreeOverrides.Load(fileName, text, LoadCatalogue(), issues);
            return issues;
        }

        private static string Patch(string upgrade, string needs, string body, string pass = "RealTPS")
        {
            return "@PARTUPGRADE[" + upgrade + "]:NEEDS[" + needs + "]:FOR[" + pass + "]\n{\n" + body + "\n}\n";
        }

        [Fact]
        public void ShippedProfilesOmitTheSchemaVersionAndTheStockUpgradesUseTheProfileNode()
        {
            foreach (string file in Directory.GetFiles(Path.Combine(GameData, "Materials"), "*.cfg"))
            {
                Assert.DoesNotContain("schemaVersion", File.ReadAllText(file));
            }
            MaterialCatalogue catalogue = LoadCatalogue();
            CfgNode upgrades = CfgParser.Parse(Read(GeneratedFiles.Unlocks));
            foreach (CfgNode node in upgrades.Nodes)
            {
                Assert.StartsWith("PARTUPGRADE", node.Name);
                MaterialDefinition m = catalogue.Materials.Single(x => x.UpgradeId == node.GetValue("name"));
                Assert.Equal(m.TechNode, node.GetValue("techRequired"));
            }
        }

        [Fact]
        public void HostPatchesEnrolNamedPartsOnlyAndNeedThePluginAndB9()
        {
            CfgNode root = CfgParser.Parse(Read(GeneratedFiles.StockHosts));
            Assert.NotEmpty(root.Nodes);
            foreach (CfgNode patch in root.Nodes)
            {
                Assert.StartsWith("@PART[", patch.Name);
                string target = patch.Name.Substring(6, patch.Name.IndexOf(']') - 6);
                Assert.DoesNotContain("*", target);
                Assert.DoesNotContain("?", target);
                Assert.DoesNotContain(",", target);
                Assert.DoesNotContain("|", target);
                Assert.DoesNotContain(":HAS[", patch.Name);
                Assert.Contains(":NEEDS[RealTPSRuntime&B9PartSwitch", patch.Name);
                Assert.Contains(":FOR[RealTPS]", patch.Name);
            }
        }

        [Fact]
        public void EveryHostHasOneRtpsModuleOneSwitcherAndAnUnrestrictedBaseline()
        {
            MaterialCatalogue catalogue = LoadCatalogue();
            CfgNode root = CfgParser.Parse(Read(GeneratedFiles.StockHosts));
            foreach (CfgNode patch in root.Nodes)
            {
                List<CfgNode> modules = patch.GetNodes("MODULE");
                CfgNode shield = modules.Single(m => m.GetValue("name") == PatchGenerator.ModuleName);
                CfgNode switcher = modules.Single(m => m.GetValue("name") == "ModuleB9PartSwitch");
                Assert.Equal(shield.GetValue("switcherModuleId"), switcher.GetValue("moduleID"));
                Assert.Equal("false", switcher.GetValue("switchInFlight"));

                List<CfgNode> subtypes = switcher.Nodes.Where(n => n.Name.StartsWith("SUBTYPE", StringComparison.Ordinal)).ToList();
                Assert.NotEmpty(subtypes);
                Assert.Equal(shield.GetValue("materialId"), subtypes[0].GetValue("name"));
                Assert.Null(subtypes[0].GetValue("upgradeRequired"));
                Assert.Equal("SUBTYPE", subtypes[0].Name);

                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (CfgNode subtype in subtypes)
                {
                    string id = subtype.GetValue("name");
                    Assert.True(names.Add(id), "duplicate subtype " + id);
                    MaterialDefinition m;
                    Assert.True(catalogue.TryGet(id, out m));
                    Assert.Equal("false", subtype.GetValue("allowSwitchInFlight"));
                    Assert.Equal(id, subtype.GetNode("MODULE").GetNode("DATA").GetValue("materialId"));
                    Assert.Equal(PatchGenerator.ModuleName, subtype.GetNode("MODULE").GetNode("IDENTIFIER").GetValue("name"));
                    Assert.Equal(m.HasConsumable, subtype.GetNode("RESOURCE") != null);
                    if (m.HasConsumable)
                    {
                        Assert.Equal(m.ConsumableResource, subtype.GetNode("RESOURCE").GetValue("name"));
                    }
                    // A speculative material must be removable by the opt-out switch, and only those.
                    Assert.Equal(m.Speculative, subtype.Name.Contains("!" + PatchGenerator.NoSpeculativeSwitch));
                }
            }
        }

        [Fact]
        public void EveryUpgradeRequirementHasAMatchingUpgradeAndViceVersa()
        {
            MaterialCatalogue catalogue = LoadCatalogue();
            CfgNode upgrades = CfgParser.Parse(Read(GeneratedFiles.Unlocks));
            var defined = new Dictionary<string, CfgNode>(StringComparer.Ordinal);
            foreach (CfgNode node in upgrades.Nodes.Where(n => n.Name.StartsWith("PARTUPGRADE", StringComparison.Ordinal)))
            {
                Assert.Contains(":NEEDS[RealTPSRuntime&B9PartSwitch", node.Name);
                Assert.StartsWith("RTPS_", node.GetValue("name"));
                Assert.False(string.IsNullOrEmpty(node.GetValue("techRequired")));
                defined.Add(node.GetValue("name"), node);
            }

            var required = new HashSet<string>(StringComparer.Ordinal);
            CfgNode hosts = CfgParser.Parse(Read(GeneratedFiles.StockHosts));
            foreach (CfgNode patch in hosts.Nodes)
            {
                CfgNode switcher = patch.GetNodes("MODULE").Single(m => m.GetValue("name") == "ModuleB9PartSwitch");
                foreach (CfgNode subtype in switcher.Nodes)
                {
                    string upgrade = subtype.GetValue("upgradeRequired");
                    if (upgrade == null)
                    {
                        continue;
                    }
                    required.Add(upgrade);
                    Assert.True(defined.ContainsKey(upgrade), "no PARTUPGRADE for " + upgrade);
                    // The unlock must exist exactly when the subtype exists.
                    bool subtypeSpeculative = subtype.Name.Contains("!" + PatchGenerator.NoSpeculativeSwitch);
                    bool upgradeSpeculative = defined[upgrade].Name.Contains("!" + PatchGenerator.NoSpeculativeSwitch);
                    Assert.Equal(subtypeSpeculative, upgradeSpeculative);
                }
            }
            Assert.Equal(required.OrderBy(s => s, StringComparer.Ordinal), defined.Keys.OrderBy(s => s, StringComparer.Ordinal));

            // Unassigned materials have no unlock.
            List<HostDefinition> hostList = LoadHosts(catalogue, new List<ValidationIssue>());
            foreach (MaterialDefinition m in catalogue.Materials.Where(m => !PatchGenerator.IsAssigned(m, hostList)))
            {
                Assert.False(defined.ContainsKey("RTPS_" + m.Id.Replace('-', '_')));
            }
        }

        [Fact]
        public void ResourcesAreOneKilogramPerUnitAndDoNotFlow()
        {
            CfgNode root = CfgParser.Parse(Read(GeneratedFiles.Resources));
            MaterialCatalogue catalogue = LoadCatalogue();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (CfgNode resource in root.GetNodes("RESOURCE_DEFINITION"))
            {
                Assert.True(names.Add(resource.GetValue("name")));
                Assert.StartsWith("Rtps", resource.GetValue("name"));
                Assert.Equal("0.001", resource.GetValue("density"));
                Assert.Equal("NO_FLOW", resource.GetValue("flowMode"));
                Assert.Equal("NONE", resource.GetValue("transfer"));
            }
            foreach (MaterialDefinition m in catalogue.Materials.Where(m => m.HasConsumable))
            {
                Assert.Contains(m.ConsumableResource, names);
            }
        }

        [Fact]
        public void StockRigidHostsRemoveTheStockAblatorAndCorrectTheCost()
        {
            MaterialCatalogue catalogue = LoadCatalogue();
            List<HostDefinition> hosts = LoadHosts(catalogue, new List<ValidationIssue>());
            string text = Read(GeneratedFiles.StockHosts);
            CfgNode root = CfgParser.Parse(text);
            foreach (HostDefinition host in hosts.Where(h => h.ReplacesStockAblator))
            {
                CfgNode patch = root.Nodes.Single(n => n.Name.StartsWith("@PART[" + host.PartName + "]", StringComparison.Ordinal));
                Assert.NotNull(patch.GetNode("!MODULE[ModuleAblator]"));
                Assert.NotNull(patch.GetNode("!RESOURCE[Ablator]"));
                Assert.Equal((host.StockAblator * 0.5).ToString(System.Globalization.CultureInfo.InvariantCulture), patch.GetValue("@cost -"));
            }
            Assert.DoesNotContain("@cost - =", text);
        }
    }
}
