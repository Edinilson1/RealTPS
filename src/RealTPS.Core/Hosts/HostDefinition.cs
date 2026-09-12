using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using RealTPS.Core.Cfg;
using RealTPS.Core.Materials;
using RealTPS.Core.Shield;

namespace RealTPS.Core.Hosts
{
    /// <summary>
    /// A part explicitly enrolled as a RealTPS host: its class, protected area, capabilities and the ordered list
    /// of materials it may carry. The first material is the baseline and must need no unlock.
    /// </summary>
    public sealed class HostDefinition
    {
        public const string NodeName = "RTPS_HOST";

        /// <summary>Internal part name exactly as in the part config (<c>name = ...</c>).</summary>
        public string PartName;

        /// <summary>Host class token, for example <c>rigid_disc</c>.</summary>
        public string HostClass;

        /// <summary>Protected area, m^2.</summary>
        public double Area;

        /// <summary>Capabilities the part offers (for example <c>hot_structure</c>).</summary>
        public List<string> Capabilities = new List<string>();

        /// <summary>Material ids in display order; index 0 is the baseline.</summary>
        public List<string> Materials = new List<string>();

        /// <summary>Stock ablator declared by the original part, kg (0 when it has none). Used for cost correction.</summary>
        public double StockAblator;

        /// <summary>ModuleManager <c>:NEEDS[]</c> condition for parts from DLC or other mods; empty for base-game parts.</summary>
        public string Needs = string.Empty;

        /// <summary>True when the original part has a stock <c>ModuleAblator</c> that the patch must replace.</summary>
        public bool ReplacesStockAblator;

        /// <summary>Name of the char colour module on the part, or empty.</summary>
        public string CharModuleName = string.Empty;

        /// <summary>
        /// Host design thickness relative to each material's reference thickness (1 = reference). Integral pod
        /// coatings are thinner than a dedicated shield.
        /// </summary>
        public double ThicknessFactor = 1.0;

        /// <summary>Non-consumable TPS mass per area already included in the original part mass, kg/m^2.</summary>
        public double IncludedDryArealDensity;

        /// <summary>
        /// True when RealTPS applies the stock-style reentry conductivity toggle to <c>part.heatConductivity</c>.
        /// False when another module on the part already owns that field (Making History pods).
        /// </summary>
        public bool OwnsConductivity = true;

        /// <summary>Human-readable part title used in generated comments.</summary>
        public string Title = string.Empty;

        // ----- protected face and its grid -----

        public int Sectors = ShieldLayout.DefaultSectors;
        public int Rings = ShieldLayout.DefaultRings;
        public double CenterPercent = ShieldLayout.DefaultCenterPercent;

        /// <summary>Outward normal of the protected face, part coordinates.</summary>
        public double[] ProtectedFace = { 0.0, -1.0, 0.0 };

        /// <summary>Direction on the face towards the middle of sector zero, part coordinates.</summary>
        public double[] SectorZero = { 1.0, 0.0, 0.0 };

        private static readonly Regex TokenPattern = new Regex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);
        private static readonly Regex PartPattern = new Regex("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant);

        /// <summary>Parses one host node. Returns null when any error was reported.</summary>
        public static HostDefinition Parse(CfgNode node, List<ValidationIssue> issues)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            var host = new HostDefinition();
            host.PartName = (node.GetValue("part") ?? string.Empty).Trim();
            string subject = "host '" + host.PartName + "'";
            int before = CountErrors(issues);

            if (!PartPattern.IsMatch(host.PartName))
            {
                issues.Add(new ValidationIssue(true, subject, "part must be one exact internal part name (wildcards are not accepted)"));
            }
            host.HostClass = (node.GetValue("hostClass") ?? string.Empty).Trim();
            if (!TokenPattern.IsMatch(host.HostClass))
            {
                issues.Add(new ValidationIssue(true, subject, "hostClass must be a plain identifier"));
            }
            if (!node.TryGetDouble("area", out host.Area) || host.Area <= 0.0 || host.Area > 10000.0)
            {
                issues.Add(new ValidationIssue(true, subject, "area must be a positive number of square metres"));
            }
            host.Capabilities = node.GetList("capabilities");
            host.Materials = node.GetList("materials");
            host.Needs = (node.GetValue("needs") ?? string.Empty).Trim();
            host.CharModuleName = (node.GetValue("charModuleName") ?? string.Empty).Trim();
            node.TryGetDouble("stockAblator", out host.StockAblator);
            host.Title = (node.GetValue("title") ?? host.PartName).Trim();
            if (node.HasValue("thicknessFactor") && (!node.TryGetDouble("thicknessFactor", out host.ThicknessFactor) || host.ThicknessFactor < 0.05 || host.ThicknessFactor > 20.0))
            {
                issues.Add(new ValidationIssue(true, subject, "thicknessFactor must be between 0.05 and 20"));
            }
            if (node.HasValue("includedDryArealDensity") && (!node.TryGetDouble("includedDryArealDensity", out host.IncludedDryArealDensity) || host.IncludedDryArealDensity < 0.0))
            {
                issues.Add(new ValidationIssue(true, subject, "includedDryArealDensity must be zero or positive"));
            }
            host.OwnsConductivity = !string.Equals((node.GetValue("ownsConductivity") ?? "true").Trim(), "false", StringComparison.OrdinalIgnoreCase);
            ParseGeometry(node, host, issues, subject);
            host.ReplacesStockAblator = string.Equals((node.GetValue("replacesStockAblator") ?? "false").Trim(), "true", StringComparison.OrdinalIgnoreCase);

            if (host.Materials.Count == 0)
            {
                issues.Add(new ValidationIssue(true, subject, "materials must list at least one material id; an empty list never means 'all'"));
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < host.Materials.Count; i++)
            {
                if (!seen.Add(host.Materials[i]))
                {
                    issues.Add(new ValidationIssue(true, subject, "material '" + host.Materials[i] + "' is listed twice"));
                }
            }
            return CountErrors(issues) == before ? host : null;
        }

        private static void ParseGeometry(CfgNode node, HostDefinition host, List<ValidationIssue> issues, string subject)
        {
            double sectors = host.Sectors;
            double rings = host.Rings;
            if ((node.HasValue("sectors") && !node.TryGetDouble("sectors", out sectors))
                || (node.HasValue("rings") && !node.TryGetDouble("rings", out rings))
                || (node.HasValue("centerPercent") && !node.TryGetDouble("centerPercent", out host.CenterPercent))
                || sectors != Math.Floor(sectors) || rings != Math.Floor(rings)
                || ShieldLayout.Parse(Compatibility.Format(sectors, "0") + "," + Compatibility.Format(rings, "0") + "," + Compatibility.Format(host.CenterPercent, "R")) == null)
            {
                issues.Add(new ValidationIssue(true, subject, "sectors (" + ShieldLayout.MinSectors + "-" + ShieldLayout.MaxSectors + "), rings (0-" + ShieldLayout.MaxRings + ") and centerPercent (0-100) do not form a valid grid"));
            }
            else
            {
                host.Sectors = (int)sectors;
                host.Rings = (int)rings;
            }

            if (node.HasValue("protectedFace") && !ShieldGeometry.TryParseVector(node.GetValue("protectedFace"), out host.ProtectedFace))
            {
                issues.Add(new ValidationIssue(true, subject, "protectedFace must be three numbers 'x, y, z'"));
            }
            if (node.HasValue("sectorZero") && !ShieldGeometry.TryParseVector(node.GetValue("sectorZero"), out host.SectorZero))
            {
                issues.Add(new ValidationIssue(true, subject, "sectorZero must be three numbers 'x, y, z'"));
            }
            if (host.ProtectedFace != null && host.SectorZero != null && !ShieldGeometry.IsUsableFrame(host.ProtectedFace, host.SectorZero))
            {
                issues.Add(new ValidationIssue(true, subject, "protectedFace must be a non-zero direction and sectorZero must not be parallel to it"));
            }
        }

        /// <summary>The grid of the protected face.</summary>
        public ShieldLayout Layout()
        {
            return new ShieldLayout(Sectors, Rings, CenterPercent);
        }

        /// <summary>Consumable inventory of a material on this host, kg.</summary>
        public double ConsumableMass(MaterialDefinition material)
        {
            return material.ConsumableArealDensity * Area * ThicknessFactor;
        }

        /// <summary>
        /// Construction mass a material adds to (or removes from) the original part mass, kg. Negative when the
        /// material is lighter than the construction already included in the part.
        /// </summary>
        public double DryMass(MaterialDefinition material)
        {
            return (material.DryArealDensity - IncludedDryArealDensity) * Area;
        }

        /// <summary>Non-consumable construction cost of a material on this host, funds.</summary>
        public double DryCost(MaterialDefinition material)
        {
            return material.CostPerArea * Area;
        }

        private static int CountErrors(List<ValidationIssue> issues)
        {
            int n = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].IsError)
                {
                    n++;
                }
            }
            return n;
        }
    }

    /// <summary>Host/material compatibility rules shared by the generator, the validators and the runtime.</summary>
    public static class Compatibility
    {
        /// <summary>
        /// Returns null when <paramref name="material"/> may be carried by a host of this class with these
        /// capabilities, otherwise the reason it may not.
        /// </summary>
        public static string Check(MaterialDefinition material, string hostClass, IList<string> hostCapabilities)
        {
            if (material == null)
            {
                return "unknown material";
            }
            if (string.IsNullOrEmpty(hostClass) || !material.PermittedHostClasses.Contains(hostClass))
            {
                return "material '" + material.Id + "' does not permit host class '" + hostClass + "'";
            }
            for (int i = 0; i < material.RequiredHostCapabilities.Count; i++)
            {
                string capability = material.RequiredHostCapabilities[i];
                if (hostCapabilities == null || !hostCapabilities.Contains(capability))
                {
                    return "material '" + material.Id + "' requires host capability '" + capability + "'";
                }
            }
            return null;
        }

        /// <summary>Validates every assignment of a host against the catalogue and the baseline rule.</summary>
        public static void ValidateHost(HostDefinition host, MaterialCatalogue catalogue, List<ValidationIssue> issues)
        {
            string subject = "host '" + host.PartName + "'";
            for (int i = 0; i < host.Materials.Count; i++)
            {
                MaterialDefinition material;
                if (!catalogue.TryGet(host.Materials[i], out material))
                {
                    issues.Add(new ValidationIssue(true, subject, "assigned material '" + host.Materials[i] + "' is not a valid catalogue entry"));
                    continue;
                }
                string reason = Check(material, host.HostClass, host.Capabilities);
                if (reason != null)
                {
                    issues.Add(new ValidationIssue(true, subject, reason));
                }
                if (i == 0 && material.TechNode.Length > 0)
                {
                    issues.Add(new ValidationIssue(true, subject, "baseline material '" + material.Id + "' must not require an unlock"));
                }
                if (material.HasConsumable && host.ConsumableMass(material) < 0.001)
                {
                    issues.Add(new ValidationIssue(true, subject, "material '" + material.Id + "' would carry less than 1 g on this host"));
                }
            }
        }

        public static string Format(double value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }
    }
}
