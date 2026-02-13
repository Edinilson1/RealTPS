using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using RealTPS.Core.Cfg;

namespace RealTPS.Core.Materials
{
    /// <summary>One validation message. Errors make a profile unusable; warnings do not.</summary>
    public sealed class ValidationIssue
    {
        public bool IsError { get; }
        public string Subject { get; }
        public string Message { get; }

        public ValidationIssue(bool isError, string subject, string message)
        {
            IsError = isError;
            Subject = subject;
            Message = message;
        }

        public override string ToString()
        {
            return (IsError ? "ERROR " : "WARN  ") + Subject + ": " + Message;
        }
    }

    /// <summary>
    /// Parses and validates <c>RTPS_MATERIAL</c> nodes (schema version 1). A node with any error yields no
    /// definition: invented defaults are never loaded into the runtime.
    /// </summary>
    public static class MaterialParser
    {
        public const string NodeName = "RTPS_MATERIAL";

        private static readonly Regex IdPattern = new Regex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);
        private static readonly Regex ResourcePattern = new Regex("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);
        private static readonly Regex TechNodePattern = new Regex("^[A-Za-z][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant);
        private static readonly Regex TokenPattern = new Regex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Parses a catalogue profile. Returns the definition, or null when <paramref name="issues"/> received
        /// at least one error.
        /// </summary>
        public static MaterialDefinition Parse(CfgNode node, List<ValidationIssue> issues)
        {
            return ParseCore(node, issues, false);
        }

        private static MaterialDefinition ParseCore(CfgNode node, List<ValidationIssue> issues, bool snapshot)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            string id = node.GetValue("id") ?? string.Empty;
            string subject = "material '" + id + "'";
            int before = CountErrors(issues);
            var m = new MaterialDefinition();

            if (!IdPattern.IsMatch(id))
            {
                Error(issues, subject, "id must be lower-case words joined by '-' (got '" + id + "')");
            }
            m.Id = id;
            RejectDuplicateKeys(node, issues, subject);
            if (!snapshot)
            {
                RejectUnknownKeys(node, issues, subject);
            }

            m.DisplayName = node.GetValue("displayName") ?? id;
            m.Description = node.GetValue("description") ?? string.Empty;

            if (!TryParseFamily(node.GetValue("family"), out m.Family))
            {
                Error(issues, subject, "family must be rigid_ablative, rigid_reusable, flexible_insulating or deployable_radiative");
            }

            m.PermittedHostClasses = ReadTokens(node, "permittedHostClasses", issues, subject);
            if (m.PermittedHostClasses.Count == 0)
            {
                Error(issues, subject, "permittedHostClasses must name at least one host class (an empty list is never a wildcard)");
            }
            m.RequiredHostCapabilities = ReadTokens(node, "requiredHostCapabilities", issues, subject);

            m.Density = Required(node, "density", 1.0, 20000.0, issues, subject, snapshot);
            m.DesignThickness = Required(node, "designThickness", 0.0005, 1.0, issues, subject, snapshot);
            m.DryArealDensity = Optional(node, "dryArealDensity", 0.0, 0.0, 500.0, issues, subject, snapshot);
            m.SpecificHeat = Required(node, "specificHeat", 100.0, 5000.0, issues, subject, snapshot);
            m.Emissivity = Required(node, "emissivity", 0.01, 1.0, issues, subject, snapshot);
            m.CharEmissivity = Optional(node, "charEmissivity", m.Emissivity, 0.01, 1.0, issues, subject, snapshot);
            m.CharSurfaceFraction = Optional(node, "charSurfaceFraction", 0.05, 0.001, 1.0, issues, subject, snapshot);
            m.MaxSurfaceTemp = Required(node, "maxSurfaceTemp", 400.0, 6000.0, issues, subject, snapshot);
            m.CostPerKg = Optional(node, "costPerKg", 0.0, 0.0, 1.0e6, issues, subject, snapshot);
            m.CostPerArea = Optional(node, "costPerArea", 0.0, 0.0, 1.0e7, issues, subject, snapshot);
            m.UnlockCost = Optional(node, "unlockCost", 0.0, 0.0, 1.0e9, issues, subject, snapshot);

            m.TechNode = (node.GetValue("techNode") ?? string.Empty).Trim();
            if (m.TechNode.Length > 0 && !TechNodePattern.IsMatch(m.TechNode))
            {
                Error(issues, subject, "techNode must be one tech node id");
            }

            List<CfgNode> ablationNodes = node.GetNodes("ABLATION");
            List<CfgNode> erosionNodes = node.GetNodes("EROSION");
            if (ablationNodes.Count > 1 || erosionNodes.Count > 1)
            {
                Error(issues, subject, "at most one ABLATION and one EROSION node are allowed");
            }
            if (ablationNodes.Count == 1)
            {
                m.Ablation = ParseAblation(ablationNodes[0], issues, subject, snapshot);
            }

            ValidateFamilyRules(m, issues, subject, snapshot);

            return CountErrors(issues) == before ? m : null;
        }

        private static AblationChannel ParseAblation(CfgNode node, List<ValidationIssue> issues, string subject, bool snapshot)
        {
            var a = new AblationChannel();
            a.Resource = (node.GetValue("resource") ?? string.Empty).Trim();
            if (!ResourcePattern.IsMatch(a.Resource))
            {
                Error(issues, subject, "ABLATION.resource must be a resource name");
            }
            a.OnsetTemp = Required(node, "onsetTemp", 300.0, 4000.0, issues, subject + " ABLATION", snapshot);
            a.ActivationTemp = Required(node, "activationTemp", 100.0, 100000.0, issues, subject + " ABLATION", snapshot);
            a.RateCoefficient = Required(node, "rateCoefficient", 1.0e-9, 1.0e9, issues, subject + " ABLATION", snapshot);
            a.HeatOfAblation = Required(node, "heatOfAblation", 1.0e4, 5.0e8, issues, subject + " ABLATION", snapshot);
            return a;
        }

        private static void ValidateFamilyRules(MaterialDefinition m, List<ValidationIssue> issues, string subject, bool snapshot)
        {
            if (m.Family == ConstructionType.RigidAblative && m.Ablation == null)
            {
                Error(issues, subject, "rigid_ablative materials need an ABLATION node");
            }
            if (m.Family != ConstructionType.RigidAblative && m.Ablation != null)
            {
                Error(issues, subject, "only rigid_ablative materials may declare an ABLATION node; reusable, flexible and deployable families must not consume material for cooling");
            }
            if (m.Ablation != null)
            {
                if (m.Ablation.OnsetTemp >= m.MaxSurfaceTemp)
                {
                    Error(issues, subject, "ABLATION.onsetTemp must be below maxSurfaceTemp");
                }
                if (!snapshot && m.CostPerKg <= 0.0)
                {
                    Warn(issues, subject, "consumable material has no costPerKg");
                }
            }
        }

        private static readonly HashSet<string> MaterialKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "schemaVersion", "id", "displayName", "description", "family", "speculative",
            "permittedHostClasses", "requiredHostCapabilities", "density", "designThickness", "dryArealDensity",
            "specificHeat", "emissivity", "charEmissivity", "charSurfaceFraction", "maxSurfaceTemp",
            "reuseSurfaceTemp", "damageSurfaceTemp", "conductivity", "bondLimitTemp",
            "costPerKg", "costPerArea", "unlockCost", "techNode"
        };

        private static readonly HashSet<string> AblationKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "resource", "onsetTemp", "activationTemp", "rateCoefficient", "heatOfAblation"
        };

        /// <summary>
        /// A catalogue profile may only contain known keys. A misspelt key must not pass silently: a wrong
        /// spelling of <c>techNode</c>, for example, would make a material free.
        /// </summary>
        private static void RejectUnknownKeys(CfgNode node, List<ValidationIssue> issues, string subject)
        {
            RejectUnknownKeys(node, MaterialKeys, issues, subject);
            for (int i = 0; i < node.Nodes.Count; i++)
            {
                CfgNode child = node.Nodes[i];
                if (child.Name == "ABLATION")
                {
                    RejectUnknownKeys(child, AblationKeys, issues, subject + " ABLATION");
                }
                else
                {
                    Error(issues, subject, "unknown node '" + child.Name + "'");
                }
            }
        }

        private static void RejectUnknownKeys(CfgNode node, HashSet<string> known, List<ValidationIssue> issues, string subject)
        {
            for (int i = 0; i < node.Values.Count; i++)
            {
                if (!known.Contains(node.Values[i].Key))
                {
                    Error(issues, subject, "unknown key '" + node.Values[i].Key + "'");
                }
            }
        }

        private static void RejectDuplicateKeys(CfgNode node, List<ValidationIssue> issues, string subject)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < node.Values.Count; i++)
            {
                if (!seen.Add(node.Values[i].Key))
                {
                    Error(issues, subject, "duplicate key '" + node.Values[i].Key + "'");
                }
            }
        }

        private static List<string> ReadTokens(CfgNode node, string key, List<ValidationIssue> issues, string subject)
        {
            List<string> list = node.GetList(key);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == "*" || !TokenPattern.IsMatch(list[i]))
                {
                    Error(issues, subject, key + " entry '" + list[i] + "' is not a plain identifier (wildcards are not accepted)");
                }
            }
            return list;
        }

        private static double Required(CfgNode node, string key, double min, double max, List<ValidationIssue> issues, string subject, bool snapshot)
        {
            double value;
            if (!node.TryGetDouble(key, out value))
            {
                Error(issues, subject, key + " is required and must be a finite number");
                return 0.0;
            }
            return CheckRange(key, value, min, max, issues, subject, snapshot);
        }

        private static double Optional(CfgNode node, string key, double fallback, double min, double max, List<ValidationIssue> issues, string subject, bool snapshot)
        {
            if (!node.HasValue(key))
            {
                return fallback;
            }
            double value;
            if (!node.TryGetDouble(key, out value))
            {
                Error(issues, subject, key + " must be a finite number");
                return fallback;
            }
            return CheckRange(key, value, min, max, issues, subject, snapshot);
        }

        private static double CheckRange(string key, double value, double min, double max, List<ValidationIssue> issues, string subject, bool snapshot)
        {
            if (snapshot)
            {
                // Numerical safety only: required quantities (min > 0) must be positive, optional ones non-negative.
                if (value < 0.0 || (min > 0.0 && value <= 0.0))
                {
                    Error(issues, subject, key + " = " + Format(value) + " is not a usable value");
                }
                return value;
            }
            if (value < min || value > max)
            {
                Error(issues, subject, key + " = " + Format(value) + " is outside the accepted range [" + Format(min) + ", " + Format(max) + "]");
            }
            return value;
        }

        public static bool TryParseFamily(string raw, out ConstructionType family)
        {
            switch (raw)
            {
                case "rigid_ablative": family = ConstructionType.RigidAblative; return true;
                case "rigid_reusable": family = ConstructionType.RigidReusable; return true;
                default: family = ConstructionType.RigidAblative; return false;
            }
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

        private static void Error(List<ValidationIssue> issues, string subject, string message)
        {
            issues.Add(new ValidationIssue(true, subject, message));
        }

        private static void Warn(List<ValidationIssue> issues, string subject, string message)
        {
            issues.Add(new ValidationIssue(false, subject, message));
        }

        private static string Format(double value)
        {
            return value.ToString("G6", CultureInfo.InvariantCulture);
        }
    }
}
