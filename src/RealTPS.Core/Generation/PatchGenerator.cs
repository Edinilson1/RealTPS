using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RealTPS.Core.Hosts;
using RealTPS.Core.Materials;

namespace RealTPS.Core.Generation
{
    /// <summary>
    /// Produces the ModuleManager configuration that enrols hosts, from the material catalogue and the host
    /// definitions. Generated text is deterministic so that a test can prove the committed files are current.
    /// </summary>
    public static class PatchGenerator
    {
        /// <summary>Assembly name of the RealTPS plugin; ModuleManager <c>:NEEDS</c> tests this, not the folder.</summary>
        public const string RuntimeAssembly = "RealTPSRuntime";

        /// <summary>ModuleManager pass name of RealTPS's own patches.</summary>
        public const string PassName = "RealTPS";

        public const string ModuleName = "ModuleRtpsShield";
        public const string SwitcherModuleId = "rtpsMaterial";
        public const string Needs = RuntimeAssembly + "&B9PartSwitch";

        private const string Header =
            "// GENERATED FILE - do not edit by hand.\n" +
            "// Source: GameData/RealTPS/Materials/*.cfg and data/hosts/*.cfg\n" +
            "// Regenerate with: dotnet run --project tools/RtpsTool -- generate\n";

        /// <summary>Localization tag stem of a material, e.g. <c>#LOC_RTPS_mat_pica</c>.</summary>
        public static string TagStem(MaterialDefinition m)
        {
            return "#LOC_RTPS_mat_" + m.Id.Replace('-', '_');
        }

        public static string ResourceTag(string resource)
        {
            return "#LOC_RTPS_res_" + resource;
        }

        /// <summary>True when at least one host carries the material.</summary>
        public static bool IsAssigned(MaterialDefinition m, IList<HostDefinition> hosts)
        {
            for (int i = 0; i < hosts.Count; i++)
            {
                if (hosts[i].Materials.Contains(m.Id))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary><c>RESOURCE_DEFINITION</c> nodes for every RealTPS-owned consumable resource in the catalogue.</summary>
        public static string Resources(MaterialCatalogue catalogue)
        {
            var sb = new StringBuilder(Header);
            sb.Append("//\n// One resource per consumable material. 1 unit = 1 kg; hsp is the material's specific heat in J/(kg K).\n");
            sb.Append("// These definitions create no inventory by themselves.\n");
            foreach (MaterialDefinition m in catalogue.Materials)
            {
                string resource = m.ConsumableResource;
                if (resource == null || !resource.StartsWith("Rtps", StringComparison.Ordinal))
                {
                    continue;
                }
                sb.Append("\nRESOURCE_DEFINITION\n{\n");
                Line(sb, 1, "name", resource);
                Line(sb, 1, "displayName", ResourceTag(resource));
                Line(sb, 1, "abbreviation", ResourceTag(resource) + "_abbr");
                Line(sb, 1, "density", "0.001");
                Line(sb, 1, "hsp", F(m.SpecificHeat));
                Line(sb, 1, "flowMode", "NO_FLOW");
                Line(sb, 1, "transfer", "NONE");
                Line(sb, 1, "isTweakable", "true");
                Line(sb, 1, "isVisible", "true");
                Line(sb, 1, "unitCost", F(m.CostPerKg));
                Line(sb, 1, "volume", "1");
                sb.Append("\tRESOURCE_DRAIN_DEFINITION\n\t{\n");
                Line(sb, 2, "isDrainable", "false");
                Line(sb, 2, "showDrainFX", "false");
                sb.Append("\t}\n}\n");
            }
            return sb.ToString();
        }

        /// <summary><c>PARTUPGRADE</c> nodes for assigned, unlockable materials, on their stock tech node.</summary>
        public static string Upgrades(MaterialCatalogue catalogue, IList<HostDefinition> hosts)
        {
            var sb = new StringBuilder(Header);
            sb.Append("//\n// One unlock per assigned material that is not a baseline. Unassigned materials get none.\n");
            foreach (MaterialDefinition m in catalogue.Materials)
            {
                if (m.UpgradeId.Length == 0 || !IsAssigned(m, hosts))
                {
                    continue;
                }
                string hostNeeds = UnlockNeeds(m, hosts);
                string needs = Needs + (hostNeeds.Length > 0 ? "&" + hostNeeds : string.Empty);
                sb.Append("\nPARTUPGRADE:NEEDS[").Append(needs).Append("]\n{\n");
                Line(sb, 1, "name", m.UpgradeId);
                Line(sb, 1, "partIcon", FirstHost(m, hosts));
                Line(sb, 1, "techRequired", m.TechNode);
                Line(sb, 1, "entryCost", F(m.UnlockCost));
                Line(sb, 1, "cost", "0");
                Line(sb, 1, "title", TagStem(m) + "_title");
                Line(sb, 1, "basicInfo", "#LOC_RTPS_upgrade_basicinfo");
                Line(sb, 1, "manufacturer", "#LOC_RTPS_manufacturer");
                Line(sb, 1, "description", TagStem(m) + "_desc");
                sb.Append("}\n");
            }
            return sb.ToString();
        }

        /// <summary>The enrolment patch of every host.</summary>
        public static string Hosts(MaterialCatalogue catalogue, IList<HostDefinition> hosts)
        {
            var sb = new StringBuilder(Header);
            sb.Append("//\n// Explicit enrolment of named parts. Nothing here matches by wildcard.\n");
            sb.Append("// Without the RealTPS plugin or B9PartSwitch none of these patches apply and the parts stay stock.\n");
            for (int i = 0; i < hosts.Count; i++)
            {
                AppendHost(sb, catalogue, hosts[i]);
            }
            return sb.ToString();
        }

        private static void AppendHost(StringBuilder sb, MaterialCatalogue catalogue, HostDefinition host)
        {
            MaterialDefinition baseline;
            if (!catalogue.TryGet(host.Materials[0], out baseline))
            {
                throw new InvalidOperationException("host '" + host.PartName + "' has an unknown baseline material");
            }

            string needs = Needs + (host.Needs.Length > 0 ? "&" + host.Needs : string.Empty);
            sb.Append("\n// ").Append(host.Title).Append(" - ").Append(host.HostClass).Append(", ").Append(F(host.Area)).Append(" m^2\n");
            sb.Append("@PART[").Append(host.PartName).Append("]:NEEDS[").Append(needs).Append("]:FOR[").Append(PassName).Append("]\n{\n");

            if (host.ReplacesStockAblator)
            {
                sb.Append("\t!MODULE[ModuleAblator] {}\n");
                sb.Append("\t!RESOURCE[Ablator] {}\n");
                if (host.StockAblator > 0.0)
                {
                    // Stock part cost includes the stock ablator (0.5 funds per unit); B9 adds each subtype's own.
                    sb.Append("\t@cost -= ").Append(F(host.StockAblator * 0.5)).Append('\n');
                }
            }

            if (baseline.HasConsumable)
            {
                // The baseline inventory sits on the prefab so that B9 deterministically selects the baseline.
                sb.Append("\tRESOURCE\n\t{\n");
                Line(sb, 2, "name", baseline.ConsumableResource);
                Line(sb, 2, "amount", F(host.ConsumableMass(baseline)));
                Line(sb, 2, "maxAmount", F(host.ConsumableMass(baseline)));
                sb.Append("\t}\n");
            }

            sb.Append("\tMODULE\n\t{\n");
            Line(sb, 2, "name", ModuleName);
            Line(sb, 2, "hostClass", host.HostClass);
            Line(sb, 2, "hostCapabilities", string.Join(", ", host.Capabilities.ToArray()));
            Line(sb, 2, "tpsArea", F(host.Area));
            Line(sb, 2, "thicknessFactor", F(host.ThicknessFactor));
            Line(sb, 2, "includedDryArealDensity", F(host.IncludedDryArealDensity));
            Line(sb, 2, "ownsConductivity", host.OwnsConductivity ? "true" : "false");
            Line(sb, 2, "charModuleName", host.CharModuleName);
            Line(sb, 2, "switcherModuleId", SwitcherModuleId);
            Line(sb, 2, "materialId", baseline.Id);
            sb.Append("\t}\n");

            sb.Append("\tMODULE\n\t{\n");
            Line(sb, 2, "name", "ModuleB9PartSwitch");
            Line(sb, 2, "moduleID", SwitcherModuleId);
            Line(sb, 2, "switcherDescription", "#LOC_RTPS_switcher");
            Line(sb, 2, "switcherDescriptionPlural", "#LOC_RTPS_switcher_plural");
            Line(sb, 2, "affectDragCubes", "false");
            Line(sb, 2, "affectFARVoxels", "false");
            Line(sb, 2, "switchInFlight", "false");
            for (int i = 0; i < host.Materials.Count; i++)
            {
                MaterialDefinition m;
                if (!catalogue.TryGet(host.Materials[i], out m))
                {
                    throw new InvalidOperationException("host '" + host.PartName + "' lists unknown material '" + host.Materials[i] + "'");
                }
                AppendSubtype(sb, host, m, i);
            }
            sb.Append("\t}\n}\n");
        }

        private static void AppendSubtype(StringBuilder sb, HostDefinition host, MaterialDefinition m, int index)
        {
            sb.Append("\t\tSUBTYPE\n\t\t{\n");
            Line(sb, 3, "name", m.Id);
            Line(sb, 3, "title", TagStem(m) + "_title");
            Line(sb, 3, "descriptionSummary", TagStem(m) + "_desc");
            Line(sb, 3, "descriptionDetail", TagStem(m) + "_detail");
            // The baseline has the highest priority so that tank-less hosts also default to it.
            Line(sb, 3, "defaultSubtypePriority", (index == 0 ? 100 : 50 - index).ToString(CultureInfo.InvariantCulture));
            if (index > 0 && m.UpgradeId.Length > 0)
            {
                Line(sb, 3, "upgradeRequired", m.UpgradeId);
            }
            Line(sb, 3, "addedMass", F(host.DryMass(m) * 0.001));
            Line(sb, 3, "addedCost", F(host.DryCost(m)));
            Line(sb, 3, "allowSwitchInFlight", "false");
            Line(sb, 3, "allowSwitchFromInFlight", "false");
            if (m.HasConsumable)
            {
                Line(sb, 3, "volumeAdded", F(host.ConsumableMass(m)));
                sb.Append("\t\t\tRESOURCE\n\t\t\t{\n");
                Line(sb, 4, "name", m.ConsumableResource);
                Line(sb, 4, "unitsPerVolume", "1");
                sb.Append("\t\t\t}\n");
            }
            sb.Append("\t\t\tMODULE\n\t\t\t{\n");
            sb.Append("\t\t\t\tIDENTIFIER\n\t\t\t\t{\n");
            Line(sb, 5, "name", ModuleName);
            sb.Append("\t\t\t\t}\n\t\t\t\tDATA\n\t\t\t\t{\n");
            Line(sb, 5, "materialId", m.Id);
            sb.Append("\t\t\t\t}\n\t\t\t}\n\t\t}\n");
        }

        /// <summary>
        /// The part shown on the unlock: the first host that is always installed (no <c>needs</c>), so the icon
        /// never names a part of a mod that may be missing; only when no such host offers the material, the first
        /// host of all.
        /// </summary>
        private static string FirstHost(MaterialDefinition m, IList<HostDefinition> hosts)
        {
            string any = string.Empty;
            for (int i = 0; i < hosts.Count; i++)
            {
                if (!hosts[i].Materials.Contains(m.Id))
                {
                    continue;
                }
                if (hosts[i].Needs.Length == 0)
                {
                    return hosts[i].PartName;
                }
                if (any.Length == 0)
                {
                    any = hosts[i].PartName;
                }
            }
            return any;
        }

        /// <summary>
        /// Extra ModuleManager condition of a material's unlock: empty when a host without <c>needs</c> offers the
        /// material; otherwise the <c>needs</c> of every host that offers it, joined with '|' (any of them), so that
        /// no unlock appears for a material that no installed part can carry. ModuleManager reads '&amp;' before
        /// '|', so the join is exact when each such host names a single mod.
        /// </summary>
        public static string UnlockNeeds(MaterialDefinition m, IList<HostDefinition> hosts)
        {
            var needs = new List<string>();
            for (int i = 0; i < hosts.Count; i++)
            {
                if (!hosts[i].Materials.Contains(m.Id))
                {
                    continue;
                }
                if (hosts[i].Needs.Length == 0)
                {
                    return string.Empty;
                }
                if (!needs.Contains(hosts[i].Needs))
                {
                    needs.Add(hosts[i].Needs);
                }
            }
            return string.Join("|", needs.ToArray());
        }

        private static void Line(StringBuilder sb, int depth, string key, string value)
        {
            sb.Append('\t', depth).Append(key).Append(" = ").Append(value).Append('\n');
        }

        private static string F(double value)
        {
            return Math.Round(value, 6).ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
