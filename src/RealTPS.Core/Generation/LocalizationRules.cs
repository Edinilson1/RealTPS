using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using RealTPS.Core.Cfg;
using RealTPS.Core.Materials;

namespace RealTPS.Core.Generation
{
    /// <summary>
    /// Checks on the localization files (<c>Localization/*.cfg</c>). They are written by hand: nothing in them is
    /// generated and no language is named in code, so a new language is a new file and nothing else. The checks:
    /// <list type="bullet">
    /// <item>en-us is present (the language KSP requires) and is the reference;</item>
    /// <item>every other language defines the same tags, with the same <c>&lt;&lt;n&gt;&gt;</c> placeholders;</item>
    /// <item>every material has a title, a description and a detail line in every language;</item>
    /// <item>every number in a material's detail line is one of that material's values, rounded as written, so
    /// that a changed profile cannot leave a stale number on screen. The decimal separator may be '.' or ',';
    /// thousands must not be grouped.</item>
    /// </list>
    /// </summary>
    public static class LocalizationRules
    {
        public const string ReferenceLanguage = "en-us";
        public const string DetailSuffix = "_detail";

        private static readonly Regex NumberToken = new Regex(@"(?<![\d.,])\d+(?:[.,]\d+)?(?!\d)", RegexOptions.CultureInvariant);
        private static readonly Regex Placeholder = new Regex(@"<<\d+>>", RegexOptions.CultureInvariant);

        /// <summary>Every tag of every language found in the files, and what was wrong with the files themselves.</summary>
        public sealed class Table
        {
            /// <summary>Language (the node name, e.g. <c>pt-br</c>) to tag to text.</summary>
            public SortedDictionary<string, Dictionary<string, string>> Languages { get; } =
                new SortedDictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

            public List<ValidationIssue> Issues { get; } = new List<ValidationIssue>();
        }

        /// <summary>Reads <c>Localization { &lt;language&gt; { tag = text } }</c> nodes from (file name, text) pairs.</summary>
        public static Table Load(IEnumerable<KeyValuePair<string, string>> files)
        {
            var table = new Table();
            foreach (KeyValuePair<string, string> file in files)
            {
                CfgNode root = CfgParser.Parse(file.Value);
                foreach (CfgNode loc in root.GetNodes("Localization"))
                {
                    foreach (CfgNode language in loc.Nodes)
                    {
                        Dictionary<string, string> tags;
                        if (!table.Languages.TryGetValue(language.Name, out tags))
                        {
                            tags = new Dictionary<string, string>(StringComparer.Ordinal);
                            table.Languages.Add(language.Name, tags);
                        }
                        foreach (KeyValuePair<string, string> value in language.Values)
                        {
                            string subject = "localization " + language.Name + " (" + file.Key + ")";
                            if (tags.ContainsKey(value.Key))
                            {
                                table.Issues.Add(new ValidationIssue(true, subject, "tag " + value.Key + " is defined twice"));
                                continue;
                            }
                            if (value.Value.Trim().Length == 0)
                            {
                                table.Issues.Add(new ValidationIssue(true, subject, "tag " + value.Key + " is empty"));
                            }
                            tags.Add(value.Key, value.Value);
                        }
                    }
                }
            }
            return table;
        }

        /// <summary>Completeness of every language against en-us, and the materials' texts against the catalogue.</summary>
        public static List<ValidationIssue> Check(Table table, MaterialCatalogue catalogue)
        {
            var issues = new List<ValidationIssue>();
            Dictionary<string, string> reference;
            if (!table.Languages.TryGetValue(ReferenceLanguage, out reference))
            {
                issues.Add(new ValidationIssue(true, "localization", "no " + ReferenceLanguage + " file: KSP requires English"));
                return issues;
            }

            foreach (KeyValuePair<string, Dictionary<string, string>> language in table.Languages)
            {
                string subject = "localization " + language.Key;
                if (language.Key != ReferenceLanguage)
                {
                    foreach (KeyValuePair<string, string> tag in reference)
                    {
                        string text;
                        if (!language.Value.TryGetValue(tag.Key, out text))
                        {
                            // Only English is required: an untranslated tag is a warning.
                            issues.Add(new ValidationIssue(false, subject, "tag " + tag.Key + " is not translated yet (defined in " + ReferenceLanguage + ")"));
                        }
                        else if (!SamePlaceholders(tag.Value, text))
                        {
                            issues.Add(new ValidationIssue(true, subject, "tag " + tag.Key + " has placeholders " + Placeholders(text) + "; "
                                + ReferenceLanguage + " has " + Placeholders(tag.Value)));
                        }
                    }
                    foreach (string tag in language.Value.Keys)
                    {
                        if (!reference.ContainsKey(tag))
                        {
                            issues.Add(new ValidationIssue(false, subject, "tag " + tag + " is not defined in " + ReferenceLanguage));
                        }
                    }
                }

                foreach (MaterialDefinition m in catalogue.Materials)
                {
                    foreach (string tag in new[] { m.DisplayName, m.Description, PatchGenerator.TagStem(m) + DetailSuffix })
                    {
                        // Required in English; in another language a missing text is one more untranslated tag (warned above).
                        if (language.Key == ReferenceLanguage && !string.IsNullOrEmpty(tag) && tag.StartsWith("#LOC_", StringComparison.Ordinal)
                            && !language.Value.ContainsKey(tag))
                        {
                            issues.Add(new ValidationIssue(true, subject, "material '" + m.Id + "' has no text for " + tag));
                        }
                    }
                    string detail;
                    if (language.Value.TryGetValue(PatchGenerator.TagStem(m) + DetailSuffix, out detail))
                    {
                        foreach (string stale in StaleNumbers(m, detail))
                        {
                            issues.Add(new ValidationIssue(true, subject, "the detail line of '" + m.Id + "' shows " + stale
                                + ", which is none of the material's values (rounded as written): update the text"));
                        }
                    }
                }
            }
            return issues;
        }

        /// <summary>The numbers in <paramref name="detail"/> that match no value of the material.</summary>
        public static List<string> StaleNumbers(MaterialDefinition m, string detail)
        {
            var stale = new List<string>();
            List<double> values = DetailValues(m);
            foreach (Match match in NumberToken.Matches(detail ?? string.Empty))
            {
                bool found = false;
                for (int i = 0; i < values.Count && !found; i++)
                {
                    found = Matches(match.Value, values[i]);
                }
                if (!found)
                {
                    stale.Add(match.Value);
                }
            }
            return stale;
        }

        /// <summary>
        /// The values a detail line may quote, in the units players read: K, kg/m³, mm, W/(m·K), kg/m², MJ/kg, kPa.
        /// </summary>
        public static List<double> DetailValues(MaterialDefinition m)
        {
            var values = new List<double>
            {
                m.MaxSurfaceTemp,
                m.BondLimitTemp,
                m.Density,
                m.DesignThickness * 1000.0,
                m.Conductivity,
                m.Emissivity,
                m.DryArealDensity
            };
            if (m.ReuseSurfaceTemp > 0.0)
            {
                values.Add(m.ReuseSurfaceTemp);
            }
            if (m.DamageSurfaceTemp > 0.0)
            {
                values.Add(m.DamageSurfaceTemp);
            }
            if (m.HasConsumable)
            {
                values.Add(m.ConsumableArealDensity);
                values.Add(m.ConsumableArealDensity + m.DryArealDensity);
            }
            if (m.Ablation != null)
            {
                values.Add(m.Ablation.OnsetTemp);
                values.Add(m.Ablation.HeatOfAblation / 1.0e6);
            }
            if (m.Erosion != null)
            {
                if (m.Erosion.ShearLimit > 0.0)
                {
                    values.Add(m.Erosion.ShearLimit / 1000.0);
                }
                if (m.Erosion.OxidationOnsetTemp > 0.0)
                {
                    values.Add(m.Erosion.OxidationOnsetTemp);
                }
            }
            return values;
        }

        /// <summary>True when <paramref name="token"/> is <paramref name="value"/> rounded to the decimals it shows.</summary>
        public static bool Matches(string token, double value)
        {
            string normal = token.Replace(',', '.');
            double shown;
            if (!double.TryParse(normal, NumberStyles.Float, CultureInfo.InvariantCulture, out shown))
            {
                return false;
            }
            int dot = normal.IndexOf('.');
            int decimals = dot < 0 ? 0 : normal.Length - dot - 1;
            double tolerance = 0.5 * Math.Pow(10.0, -decimals) + 1.0e-9 * Math.Max(1.0, Math.Abs(value));
            return Math.Abs(value - shown) <= tolerance;
        }

        private static bool SamePlaceholders(string a, string b)
        {
            return Placeholders(a) == Placeholders(b);
        }

        private static string Placeholders(string text)
        {
            var found = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match match in Placeholder.Matches(text))
            {
                found.Add(match.Value);
            }
            return found.Count == 0 ? "none" : string.Join(" ", found);
        }
    }
}
