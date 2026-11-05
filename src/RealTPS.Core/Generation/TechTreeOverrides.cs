using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealTPS.Core.Cfg;
using RealTPS.Core.Materials;

namespace RealTPS.Core.Generation
{
    /// <summary>
    /// The unlock nodes of one alternative tech tree, written by hand in <c>TechTree/&lt;needs&gt;.cfg</c>. A material
    /// states a single <c>techNode</c> (the stock tree, used by the generated <c>MaterialUnlocks.cfg</c>); the file of
    /// a tree lists only the materials whose node changes when that tree is installed, one patch each:
    /// <code>
    /// @PARTUPGRADE[RTPS_heeet]:NEEDS[RealTPSRuntime&amp;B9PartSwitch&amp;CommunityTechTree]:FOR[RealTPS]
    /// {
    ///     @techRequired = aerospaceComposites
    /// }
    /// </code>
    /// A speculative material adds <c>&amp;!RealTPSNoSpeculative</c> to the NEEDS, as its unlock does. This class only
    /// reads and checks such a file; nothing writes it.
    /// </summary>
    public sealed class TechTreeOverrides
    {
        /// <summary>Folder of the tech tree files, relative to the repository root.</summary>
        public const string Folder = GeneratedFiles.ModFolder + "/TechTree";

        /// <summary>ModuleManager mod name present when the tree is installed; also the file name without <c>.cfg</c>.</summary>
        public string Needs = string.Empty;

        /// <summary>Material id → tech node, in file order.</summary>
        public List<KeyValuePair<string, string>> Nodes = new List<KeyValuePair<string, string>>();

        private static readonly Regex NeedsPattern = new Regex("^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant);
        private static readonly Regex NodePattern = new Regex("^[A-Za-z][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant);
        private static readonly Regex PatchName = new Regex(
            @"^@PARTUPGRADE\[(?<upgrade>[^\]]*)\]:NEEDS\[(?<needs>[^\]]*)\]:FOR\[(?<pass>[^\]]*)\]$", RegexOptions.CultureInvariant);

        /// <summary>
        /// True for a file of <see cref="Folder"/> that holds tech tree overrides: every <c>.cfg</c> there except the
        /// generated stock unlocks.
        /// </summary>
        public static bool IsOverrideFile(string fileName)
        {
            return string.Equals(Path.GetExtension(fileName), ".cfg", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Path.GetFileName(fileName), Path.GetFileName(GeneratedFiles.Unlocks), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The NEEDS an override of <paramref name="material"/> must carry for the tree <paramref name="needs"/>.</summary>
        public static string ExpectedNeeds(MaterialDefinition material, string needs)
        {
            return PatchGenerator.Needs + "&" + needs + (material.Speculative ? "&!" + PatchGenerator.NoSpeculativeSwitch : string.Empty);
        }

        /// <summary>
        /// Reads one tree file and checks every patch against the catalogue. Problems go to <paramref name="issues"/>;
        /// the entries that passed are returned (the tree's name comes from the file name).
        /// </summary>
        public static TechTreeOverrides Load(string fileName, string text, MaterialCatalogue catalogue, List<ValidationIssue> issues)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }
            if (catalogue == null)
            {
                throw new ArgumentNullException(nameof(catalogue));
            }
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            var tree = new TechTreeOverrides { Needs = Path.GetFileNameWithoutExtension(fileName) };
            string subject = "tech tree " + Path.GetFileName(fileName);
            if (!NeedsPattern.IsMatch(tree.Needs))
            {
                issues.Add(new ValidationIssue(true, subject, "the file name must be the tree's ModuleManager mod name (letters, digits, underscore)"));
                return tree;
            }

            CfgNode root = CfgParser.Parse(text ?? string.Empty);
            foreach (KeyValuePair<string, string> value in root.Values)
            {
                issues.Add(new ValidationIssue(true, subject, "'" + value.Key + "' outside a patch; the file holds only @PARTUPGRADE patches"));
            }
            if (root.Nodes.Count == 0)
            {
                issues.Add(new ValidationIssue(true, subject, "lists no material; delete the file instead"));
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (CfgNode patch in root.Nodes)
            {
                Match name = PatchName.Match(patch.Name);
                if (!name.Success)
                {
                    issues.Add(new ValidationIssue(true, subject, "'" + patch.Name + "' is not @PARTUPGRADE[RTPS_<material>]:NEEDS[...]:FOR[" + PatchGenerator.PassName + "]"));
                    continue;
                }
                string upgrade = name.Groups["upgrade"].Value;
                MaterialDefinition material = Find(catalogue, upgrade);
                if (material == null)
                {
                    issues.Add(new ValidationIssue(true, subject, "'" + upgrade + "' is not the unlock of any material (RTPS_ + material id, '-' written as '_')"));
                    continue;
                }
                string entry = subject + ", " + material.Id;
                if (material.TechNode.Length == 0)
                {
                    issues.Add(new ValidationIssue(true, entry, "the material has no techNode: a material without an unlock cannot be moved"));
                    continue;
                }
                if (!seen.Add(material.Id))
                {
                    issues.Add(new ValidationIssue(true, entry, "is listed twice"));
                    continue;
                }

                int before = issues.Count;
                string needs = ExpectedNeeds(material, tree.Needs);
                if (name.Groups["needs"].Value != needs)
                {
                    issues.Add(new ValidationIssue(true, entry, "NEEDS must be exactly [" + needs + "]"
                        + (material.Speculative ? " (a speculative material keeps !" + PatchGenerator.NoSpeculativeSwitch + ", as its unlock does)" : string.Empty)));
                }
                if (name.Groups["pass"].Value != PatchGenerator.PassName)
                {
                    issues.Add(new ValidationIssue(true, entry, "FOR must be [" + PatchGenerator.PassName + "]"));
                }

                string node = patch.GetValue("@techRequired");
                if (patch.Nodes.Count > 0 || patch.Values.Count != 1 || node == null)
                {
                    issues.Add(new ValidationIssue(true, entry, "the patch holds one line, @techRequired = <node>, and nothing else"));
                }
                else if (!NodePattern.IsMatch(node))
                {
                    issues.Add(new ValidationIssue(true, entry, "'" + node + "' is not a tech node id"));
                }
                else if (node == material.TechNode)
                {
                    issues.Add(new ValidationIssue(true, entry, "already unlocks at '" + node + "' (its techNode); list only materials whose node changes"));
                }
                if (issues.Count == before)
                {
                    tree.Nodes.Add(new KeyValuePair<string, string>(material.Id, node));
                }
            }
            return tree;
        }

        private static MaterialDefinition Find(MaterialCatalogue catalogue, string upgrade)
        {
            foreach (MaterialDefinition m in catalogue.Materials)
            {
                // UpgradeId is empty for a material without techNode; compare with the name it would have.
                if ("RTPS_" + m.Id.Replace('-', '_') == upgrade)
                {
                    return m;
                }
            }
            return null;
        }
    }
}
