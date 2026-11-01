using System;
using System.Collections.Generic;
using System.Globalization;
using RealTPS.Core.Cfg;
using RealTPS.Core.Materials;

namespace RealTPS.Core.Shield
{
    /// <summary>
    /// The difficulty presets a save can choose: three levels and Custom, which keeps whatever the player set.
    /// </summary>
    public enum DifficultyPreset
    {
        Friendly,
        Realistic,
        Strict,
        Custom
    }

    /// <summary>
    /// The values of every named preset and the preset a new game takes for each stock difficulty level.
    /// Built in, then overridden key by key by <c>RTPS_DIFFICULTY</c> nodes (after ModuleManager), so that a
    /// player or another mod can retune a preset without rebuilding. Out-of-range values are clamped with a
    /// warning; malformed or unknown entries are ignored with a warning: a bad patch never stops the game.
    /// </summary>
    public sealed class DifficultyPresets
    {
        public const string NodeName = "RTPS_DIFFICULTY";
        public const string PresetNodeName = "PRESET";

        // Ranges of every value; also the ranges of the sliders of the difficulty screen.
        public const double MinDamageRate = 0.25;
        public const double MaxDamageRate = 3.0;
        public const double MinSurfaceDamageTime = 2.0;
        public const double MaxSurfaceDamageTime = 60.0;
        public const double MinBondDamageTime = 10.0;
        public const double MaxBondDamageTime = 600.0;
        public const double MinStructureDamageTime = 2.0;
        public const double MaxStructureDamageTime = 60.0;
        public const double MaxCellVariation = 0.10;

        // Keys of a PRESET node.
        public const string KeyDamage = "damage";
        public const string KeyDamageRate = "damageRate";
        public const string KeySurfaceDamageTime = "surfaceDamageTime";
        public const string KeyBondDamageTime = "bondDamageTime";
        public const string KeyStructureDamageTime = "structureDamageTime";
        public const string KeyFailure = "failure";
        public const string KeyBreachHeat = "breachHeat";
        public const string KeyBreachShare = "breachShare";
        public const string KeyCellVariation = "cellVariation";

        /// <summary>The named presets, in the order of the difficulty screen.</summary>
        public static readonly DifficultyPreset[] Named =
        {
            DifficultyPreset.Friendly, DifficultyPreset.Realistic, DifficultyPreset.Strict
        };

        private readonly ShieldSettings[] values = new ShieldSettings[Named.Length];

        /// <summary>Preset of a new game on the stock "Easy" level.</summary>
        public DifficultyPreset Easy { get; private set; }

        /// <summary>Preset of a new game on the stock "Normal" level.</summary>
        public DifficultyPreset Normal { get; private set; }

        /// <summary>Preset of a new game on the stock "Moderate" level.</summary>
        public DifficultyPreset Moderate { get; private set; }

        /// <summary>Preset of a new game on the stock "Hard" level.</summary>
        public DifficultyPreset Hard { get; private set; }

        /// <summary>Warnings raised while reading the nodes. Never errors: the values in use are always valid.</summary>
        public List<ValidationIssue> Issues { get; } = new List<ValidationIssue>();

        private DifficultyPresets()
        {
            for (int i = 0; i < Named.Length; i++)
            {
                values[i] = BuiltInValues(Named[i]);
            }
            Easy = DifficultyPreset.Friendly;
            Normal = DifficultyPreset.Realistic;
            Moderate = DifficultyPreset.Realistic;
            Hard = DifficultyPreset.Strict;
        }

        /// <summary>True for the three presets with values of their own (not Custom, not an undefined value).</summary>
        public static bool IsNamed(DifficultyPreset preset)
        {
            return preset >= DifficultyPreset.Friendly && preset <= DifficultyPreset.Strict;
        }

        /// <summary>The presets with nothing but the built-in values.</summary>
        public static DifficultyPresets BuiltIn()
        {
            return new DifficultyPresets();
        }

        /// <summary>
        /// The values this build ships with; <c>Difficulty/DifficultyPresets.cfg</c> states the same numbers.
        /// Levels: Friendly halves the damage rates, passes on a quarter of the breach heat and has no variation
        /// between cells; Realistic is the calibrated model; Strict damages 1.5 times faster, passes on three
        /// quarters and varies cells by 5 %. The time constants are the calibrated ones at every level.
        /// </summary>
        public static ShieldSettings BuiltInValues(DifficultyPreset preset)
        {
            if (!IsNamed(preset))
            {
                throw new ArgumentOutOfRangeException(nameof(preset), "only the named presets have values of their own");
            }
            int level = (int)preset;
            return new ShieldSettings
            {
                Damage = true,
                Failure = true,
                Transfer = true,
                Severity = level == 0 ? 0.5 : (level == 1 ? 1.0 : 1.5),
                BreachShare = level == 0 ? 0.25 : (level == 1 ? 0.5 : 0.75),
                CellVariation = level == 0 ? 0.0 : (level == 1 ? 0.02 : 0.05),
                SurfaceDamageTime = ShieldSolver.SurfaceDamageTime,
                BondDamageTime = ShieldSolver.BondDamageTime,
                StructureDamageTime = ShieldSolver.StructureDamageTime
            };
        }

        /// <summary>The values of a named preset. Read-only by contract: copy before changing anything.</summary>
        public ShieldSettings Values(DifficultyPreset preset)
        {
            if (!IsNamed(preset))
            {
                throw new ArgumentOutOfRangeException(nameof(preset), "only the named presets have values of their own");
            }
            return values[(int)preset];
        }

        /// <summary>The preset a new game takes for a stock difficulty level: 0 Easy, 1 Normal, 2 Moderate, 3 Hard.</summary>
        public DifficultyPreset ForLevel(int level)
        {
            switch (level)
            {
                case 0: return Easy;
                case 2: return Moderate;
                case 3: return Hard;
                default: return Normal;
            }
        }

        /// <summary>Clamps every value into its range. NaN takes the calibrated default.</summary>
        public static void Clamp(ShieldSettings s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            s.Severity = Range(s.Severity, MinDamageRate, MaxDamageRate, 1.0);
            s.SurfaceDamageTime = Range(s.SurfaceDamageTime, MinSurfaceDamageTime, MaxSurfaceDamageTime, ShieldSolver.SurfaceDamageTime);
            s.BondDamageTime = Range(s.BondDamageTime, MinBondDamageTime, MaxBondDamageTime, ShieldSolver.BondDamageTime);
            s.StructureDamageTime = Range(s.StructureDamageTime, MinStructureDamageTime, MaxStructureDamageTime, ShieldSolver.StructureDamageTime);
            s.BreachShare = Range(s.BreachShare, 0.0, 1.0, ShieldSolver.BreachShare);
            s.CellVariation = Range(s.CellVariation, 0.0, MaxCellVariation, 0.0);
        }

        private static double Range(double value, double min, double max, double fallback)
        {
            if (double.IsNaN(value))
            {
                return fallback;
            }
            return value < min ? min : (value > max ? max : value);
        }

        /// <summary>
        /// The built-in presets overridden by <paramref name="nodes"/> (<c>RTPS_DIFFICULTY</c>), in order: a key
        /// that a later node states replaces the earlier value; a key no node states keeps the built-in value.
        /// </summary>
        public static DifficultyPresets Load(IEnumerable<CfgNode> nodes)
        {
            var result = new DifficultyPresets();
            if (nodes == null)
            {
                return result;
            }
            foreach (CfgNode node in nodes)
            {
                if (node != null)
                {
                    result.Apply(node);
                }
            }
            return result;
        }

        private void Apply(CfgNode node)
        {
            for (int i = 0; i < node.Values.Count; i++)
            {
                string key = node.Values[i].Key;
                string text = node.Values[i].Value;
                DifficultyPreset preset;
                switch (key)
                {
                    case "easy":
                    case "normal":
                    case "moderate":
                    case "hard":
                        if (!TryParseNamed(text, out preset))
                        {
                            Warn(NodeName, "'" + key + " = " + text + "' is not a named preset (" + NamedList() + "); kept " + ForKey(key));
                            continue;
                        }
                        if (key == "easy")
                        {
                            Easy = preset;
                        }
                        else if (key == "normal")
                        {
                            Normal = preset;
                        }
                        else if (key == "moderate")
                        {
                            Moderate = preset;
                        }
                        else
                        {
                            Hard = preset;
                        }
                        break;
                    default:
                        Warn(NodeName, "unknown key '" + key + "' ignored");
                        break;
                }
            }

            for (int n = 0; n < node.Nodes.Count; n++)
            {
                CfgNode child = node.Nodes[n];
                if (child.Name != PresetNodeName)
                {
                    Warn(NodeName, "unknown node '" + child.Name + "' ignored");
                    continue;
                }
                string name = child.GetValue("name");
                DifficultyPreset preset;
                if (name == null || !TryParseNamed(name, out preset))
                {
                    Warn(NodeName, "PRESET '" + (name ?? "(no name)") + "' is not a named preset (" + NamedList() + "); ignored");
                    continue;
                }
                ApplyPreset(values[(int)preset], child, "difficulty preset '" + preset + "'");
            }
        }

        private void ApplyPreset(ShieldSettings s, CfgNode node, string subject)
        {
            for (int i = 0; i < node.Values.Count; i++)
            {
                string key = node.Values[i].Key;
                string text = node.Values[i].Value;
                switch (key)
                {
                    case "name":
                        break;
                    case KeyDamage:
                        Bool(subject, key, text, ref s.Damage);
                        break;
                    case KeyFailure:
                        Bool(subject, key, text, ref s.Failure);
                        break;
                    case KeyBreachHeat:
                        Bool(subject, key, text, ref s.Transfer);
                        break;
                    case KeyDamageRate:
                        Number(subject, key, text, MinDamageRate, MaxDamageRate, ref s.Severity);
                        break;
                    case KeySurfaceDamageTime:
                        Number(subject, key, text, MinSurfaceDamageTime, MaxSurfaceDamageTime, ref s.SurfaceDamageTime);
                        break;
                    case KeyBondDamageTime:
                        Number(subject, key, text, MinBondDamageTime, MaxBondDamageTime, ref s.BondDamageTime);
                        break;
                    case KeyStructureDamageTime:
                        Number(subject, key, text, MinStructureDamageTime, MaxStructureDamageTime, ref s.StructureDamageTime);
                        break;
                    case KeyBreachShare:
                        Number(subject, key, text, 0.0, 1.0, ref s.BreachShare);
                        break;
                    case KeyCellVariation:
                        Number(subject, key, text, 0.0, MaxCellVariation, ref s.CellVariation);
                        break;
                    default:
                        Warn(subject, "unknown key '" + key + "' ignored");
                        break;
                }
            }
        }

        private void Bool(string subject, string key, string text, ref bool target)
        {
            bool value;
            if (text != null && bool.TryParse(text.Trim(), out value))
            {
                target = value;
            }
            else
            {
                Warn(subject, "'" + key + " = " + text + "' is not true or false; kept " + (target ? "true" : "false"));
            }
        }

        private void Number(string subject, string key, string text, double min, double max, ref double target)
        {
            double value;
            if (text == null || !double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.IsNaN(value) || double.IsInfinity(value))
            {
                Warn(subject, "'" + key + " = " + text + "' is not a number; kept " + target.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (value < min || value > max)
            {
                double clamped = value < min ? min : max;
                Warn(subject, "'" + key + " = " + text + "' is outside " + min.ToString("R", CultureInfo.InvariantCulture) + " .. "
                    + max.ToString("R", CultureInfo.InvariantCulture) + "; clamped to " + clamped.ToString("R", CultureInfo.InvariantCulture));
                value = clamped;
            }
            target = value;
        }

        /// <summary>Parses the name of a named preset, ignoring case and surrounding blanks.</summary>
        public static bool TryParseNamed(string text, out DifficultyPreset preset)
        {
            preset = DifficultyPreset.Custom;
            if (text == null)
            {
                return false;
            }
            string trimmed = text.Trim();
            for (int i = 0; i < Named.Length; i++)
            {
                if (string.Equals(trimmed, Named[i].ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    preset = Named[i];
                    return true;
                }
            }
            return false;
        }

        private DifficultyPreset ForKey(string key)
        {
            return key == "easy" ? Easy : key == "moderate" ? Moderate : key == "hard" ? Hard : Normal;
        }

        private static string NamedList()
        {
            var names = new string[Named.Length];
            for (int i = 0; i < Named.Length; i++)
            {
                names[i] = Named[i].ToString();
            }
            return string.Join(", ", names);
        }

        private void Warn(string subject, string message)
        {
            Issues.Add(new ValidationIssue(false, subject, message));
        }
    }
}
