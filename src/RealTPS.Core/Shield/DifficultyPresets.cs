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

        /// <summary>The named presets, in the order of the difficulty screen.</summary>
        public static readonly DifficultyPreset[] Named =
        {
            DifficultyPreset.Friendly, DifficultyPreset.Realistic, DifficultyPreset.Strict
        };

        private readonly ShieldSettings[] values = new ShieldSettings[Named.Length];

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
    }
}
