using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using RealTPS.Core.Cfg;
using RealTPS.Core.Materials;

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

        /// <summary>Material ids in display order; index 0 is the baseline.</summary>
        public List<string> Materials = new List<string>();

        /// <summary>Stock ablator declared by the original part, kg (0 when it has none). Used for cost correction.</summary>
        public double StockAblator;

        /// <summary>True when the original part has a stock <c>ModuleAblator</c> that the patch must replace.</summary>
        public bool ReplacesStockAblator;

        /// <summary>Name of the char colour module on the part, or empty.</summary>
        public string CharModuleName = string.Empty;

        /// <summary>Human-readable part title used in generated comments.</summary>
        public string Title = string.Empty;

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
            host.Materials = node.GetList("materials");
            host.CharModuleName = (node.GetValue("charModuleName") ?? string.Empty).Trim();
            node.TryGetDouble("stockAblator", out host.StockAblator);
            host.Title = (node.GetValue("title") ?? host.PartName).Trim();
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

        /// <summary>Consumable inventory of a material on this host, kg.</summary>
        public double ConsumableMass(MaterialDefinition material)
        {
            return material.ConsumableArealDensity * Area;
        }

        /// <summary>
        /// Construction mass a material adds to (or removes from) the original part mass, kg. Negative when the
        /// material is lighter than the construction already included in the part.
        /// </summary>
        public double DryMass(MaterialDefinition material)
        {
            return material.DryArealDensity * Area;
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
