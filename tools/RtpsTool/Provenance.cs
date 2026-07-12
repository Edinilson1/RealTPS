using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RealTPS.Core.Generation;
using RealTPS.Core.Materials;

namespace RtpsTool
{
    /// <summary>
    /// Checks the hand-written <c>docs/sources/MATERIAL_SOURCES.md</c> against the catalogue: every number that
    /// ships has one row with the same value, a status, a source key (or <c>-</c>) and a note, and no row matches
    /// nothing. Nothing is generated from it.
    /// </summary>
    internal static class Provenance
    {
        public const string DocPath = "docs/sources/MATERIAL_SOURCES.md";

        private static readonly string[] Statuses = { "SOURCED", "DERIVED", "ASSUMPTION", "CALIBRATION", "FICTIONAL" };

        private sealed class Row
        {
            public string Property;
            public string Value;
            public double? Number;
        }

        public static void Check(Repository repo)
        {
            string file = Path.Combine(repo.Root, DocPath);
            if (!File.Exists(file))
            {
                Error(repo, DocPath + " is missing");
                return;
            }

            var sources = new HashSet<string>(StringComparer.Ordinal);
            var tables = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.Ordinal);
            Dictionary<string, string[]> table = null;
            bool inSources = false;
            string[] lines = File.ReadAllText(file).Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    inSources = line == "## Sources";
                    table = null;
                    continue;
                }
                if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    string id = Unquote(line.Substring(4).Split(':')[0]);
                    if (tables.ContainsKey(id))
                    {
                        Error(repo, "material '" + id + "' has two sections");
                    }
                    table = new Dictionary<string, string[]>(StringComparer.Ordinal);
                    tables[id] = table;
                    inSources = false;
                    continue;
                }
                if (!line.StartsWith("|", StringComparison.Ordinal) || (!inSources && table == null))
                {
                    continue;
                }
                string[] cells = SplitRow(line);
                if (cells.Length == 0 || cells[0] == "Key" || cells[0] == "Property" || cells[0].Trim('-').Length == 0)
                {
                    continue;
                }
                if (inSources)
                {
                    sources.Add(Unquote(cells[0]));
                    continue;
                }
                if (cells.Length != 6)
                {
                    Error(repo, "line " + (i + 1) + " must have 6 columns: property, value, unit, status, source, note");
                    continue;
                }
                if (table.ContainsKey(cells[0]))
                {
                    Error(repo, "line " + (i + 1) + ": '" + cells[0] + "' is listed twice");
                }
                table[cells[0]] = cells;
            }

            foreach (string id in tables.Keys.Where(k => !repo.Catalogue.TryGet(k, out _)))
            {
                Error(repo, "section for unknown material '" + id + "'");
            }
            foreach (MaterialDefinition m in repo.Catalogue.Materials)
            {
                Dictionary<string, string[]> rows;
                if (!tables.TryGetValue(m.Id, out rows))
                {
                    Error(repo, "material '" + m.Id + "' has no section");
                    continue;
                }
                var used = new HashSet<string>(StringComparer.Ordinal);
                foreach (Row row in Rows(m, repo.TechTrees))
                {
                    string[] cells;
                    if (!rows.TryGetValue(row.Property, out cells))
                    {
                        Error(repo, "material '" + m.Id + "': " + row.Property + " = " + row.Value + " has no row");
                        continue;
                    }
                    used.Add(row.Property);
                    string subject = "material '" + m.Id + "', " + row.Property;
                    double number;
                    bool same = row.Number.HasValue
                        ? double.TryParse(cells[1], NumberStyles.Float, CultureInfo.InvariantCulture, out number) && SameNumber(number, row.Number.Value)
                        : cells[1] == row.Value;
                    if (!same)
                    {
                        Error(repo, subject + ": the row says " + cells[1] + ", the material has " + row.Value);
                    }
                    string status = Unquote(cells[3]);
                    string source = Unquote(cells[4]);
                    if (Array.IndexOf(Statuses, status) < 0)
                    {
                        Error(repo, subject + ": unknown status '" + status + "'");
                    }
                    if (source != "-" && !sources.Contains(source))
                    {
                        Error(repo, subject + ": unknown source '" + source + "' (not in the Sources table)");
                    }
                    if (status == "SOURCED" && source == "-")
                    {
                        Error(repo, subject + ": SOURCED without a source");
                    }
                    if (cells[5].Length == 0)
                    {
                        Error(repo, subject + ": no note");
                    }
                }
                foreach (string property in rows.Keys.Where(k => !used.Contains(k)))
                {
                    Error(repo, "material '" + m.Id + "': the row for '" + property + "' matches no value of the material");
                }
            }
        }

        private static void Error(Repository repo, string message)
        {
            repo.Issues.Add(new ValidationIssue(true, DocPath, message));
        }

        private static bool SameNumber(double a, double b)
        {
            return Math.Abs(a - b) <= 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
        }

        private static string Unquote(string cell)
        {
            return cell.Trim().Trim('`').Trim();
        }

        /// <summary>Cells of a Markdown table row; <c>\|</c> is a literal bar.</summary>
        private static string[] SplitRow(string line)
        {
            var cells = new List<string>();
            var current = new System.Text.StringBuilder();
            for (int i = 1; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\\' && i + 1 < line.Length && line[i + 1] == '|')
                {
                    current.Append('|');
                    i++;
                }
                else if (c == '|')
                {
                    cells.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            return cells.ToArray();
        }

        private static IEnumerable<Row> Rows(MaterialDefinition m, List<TechTreeOverrides> trees)
        {
            yield return Num("density", m.Density);
            yield return Num("designThickness", m.DesignThickness);
            if (m.DryArealDensity != 0.0)
            {
                yield return Num("dryArealDensity", m.DryArealDensity);
            }
            yield return Num("specificHeat", m.SpecificHeat);
            yield return Num("emissivity", m.Emissivity);
            yield return Num("charEmissivity", m.CharEmissivity);
            if (m.Ablation != null)
            {
                yield return Num("charSurfaceFraction", m.CharSurfaceFraction);
            }
            yield return Num("maxSurfaceTemp", m.MaxSurfaceTemp);
            if (m.ReuseSurfaceTemp != 0.0)
            {
                yield return Num("reuseSurfaceTemp", m.ReuseSurfaceTemp);
            }
            if (m.DamageSurfaceTemp != 0.0)
            {
                yield return Num("damageSurfaceTemp", m.DamageSurfaceTemp);
            }
            yield return Num("conductivity", m.Conductivity);
            yield return Num("bondLimitTemp", m.BondLimitTemp);
            if (m.CostPerKg != 0.0)
            {
                yield return Num("costPerKg", m.CostPerKg);
            }
            if (m.CostPerArea != 0.0)
            {
                yield return Num("costPerArea", m.CostPerArea);
            }
            if (m.UnlockCost != 0.0)
            {
                yield return Num("unlockCost", m.UnlockCost);
            }
            if (m.Ablation != null)
            {
                yield return Num("ablation.onsetTemp", m.Ablation.OnsetTemp);
                yield return Num("ablation.activationTemp", m.Ablation.ActivationTemp);
                yield return Num("ablation.rateCoefficient", m.Ablation.RateCoefficient);
                yield return Num("ablation.heatOfAblation", m.Ablation.HeatOfAblation);
            }
            if (m.Erosion != null)
            {
                if (m.Erosion.ShearLimit != 0.0)
                {
                    yield return Num("erosion.shearLimit", m.Erosion.ShearLimit);
                    yield return Num("erosion.shearRate", m.Erosion.ShearRate);
                }
                if (m.Erosion.OxidationOnsetTemp != 0.0)
                {
                    yield return Num("erosion.oxidationOnsetTemp", m.Erosion.OxidationOnsetTemp);
                    yield return Num("erosion.oxidationActivationTemp", m.Erosion.OxidationActivationTemp);
                    yield return Num("erosion.oxidationRate", m.Erosion.OxidationRate);
                }
            }
            if (m.TechNode.Length > 0)
            {
                yield return new Row { Property = "techNode", Value = m.TechNode };
            }
            foreach (TechTreeOverrides tree in trees)
            {
                foreach (KeyValuePair<string, string> entry in tree.Nodes)
                {
                    if (entry.Key == m.Id)
                    {
                        yield return new Row { Property = "techNode@" + tree.Needs, Value = entry.Value };
                    }
                }
            }
        }

        private static Row Num(string property, double value)
        {
            return new Row { Property = property, Value = value.ToString("R", CultureInfo.InvariantCulture), Number = value };
        }
    }
}
