using System;
using System.Collections;
using System.Reflection;
using RealTPS.Core.Shield;
using KSP.Localization;
using UnityEngine;

namespace RealTPS
{
    /// <summary>
    /// Per-save shield settings, in the game's difficulty options. They scale or switch parts of the damage
    /// model; they never change the material physics (consumption, sink, conduction).
    /// A named preset takes its values from <see cref="RtpsCatalogue.Difficulty"/> (the <c>RTPS_DIFFICULTY</c>
    /// node, which other mods may patch) every time the solver reads them; the sliders then only show those
    /// values. Custom uses the sliders.
    /// </summary>
    public sealed class RtpsDifficulty : GameParameters.CustomParameterNode
    {
        /// <summary>
        /// Version of the preset list written to the save. 1 (no value): Friendly / Realistic / Strict / Custom.
        /// 2: a stock family (StockFriendly ...) and a real-scale family (Friendly ...). 3: Friendly / Realistic /
        /// Strict / Custom again, with the stock time constants. Older saves load through
        /// <see cref="DifficultyPresets.MigrateSavedName"/>.
        /// </summary>
        public const int CurrentPresetVersion = 3;

        public override string Title { get { return Localizer.Format("#LOC_RTPS_settings_title"); } }
        public override string Section { get { return "RealTPS"; } }
        public override string DisplaySection { get { return Localizer.Format("#LOC_RTPS_settings_section"); } }
        public override int SectionOrder { get { return 0; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override bool HasPresets { get { return true; } }

        [GameParameters.CustomParameterUI("#LOC_RTPS_settings_preset", toolTip = "#LOC_RTPS_settings_preset_tip")]
        public DifficultyPreset preset = DifficultyPreset.Realistic;

        [GameParameters.CustomParameterUI("#LOC_RTPS_settings_damage", toolTip = "#LOC_RTPS_settings_damage_tip")]
        public bool damage = true;

        [GameParameters.CustomFloatParameterUI("#LOC_RTPS_settings_severity", minValue = 0.25f, maxValue = 3.0f, stepCount = 12, displayFormat = "F2", toolTip = "#LOC_RTPS_settings_severity_tip")]
        public float severity = 1.0f;

        [GameParameters.CustomFloatParameterUI("#LOC_RTPS_settings_surface_time", minValue = 2f, maxValue = 60f, stepCount = 59, displayFormat = "F0", toolTip = "#LOC_RTPS_settings_surface_time_tip")]
        public float surfaceDamageTime = (float)ShieldSolver.SurfaceDamageTime;

        [GameParameters.CustomFloatParameterUI("#LOC_RTPS_settings_bond_time", minValue = 10f, maxValue = 600f, stepCount = 60, displayFormat = "F0", toolTip = "#LOC_RTPS_settings_bond_time_tip")]
        public float bondDamageTime = (float)ShieldSolver.BondDamageTime;

        [GameParameters.CustomFloatParameterUI("#LOC_RTPS_settings_structure_time", minValue = 2f, maxValue = 60f, stepCount = 59, displayFormat = "F0", toolTip = "#LOC_RTPS_settings_structure_time_tip")]
        public float structureDamageTime = (float)ShieldSolver.StructureDamageTime;

        [GameParameters.CustomParameterUI("#LOC_RTPS_settings_failure", toolTip = "#LOC_RTPS_settings_failure_tip")]
        public bool failure = true;

        [GameParameters.CustomParameterUI("#LOC_RTPS_settings_transfer", toolTip = "#LOC_RTPS_settings_transfer_tip")]
        public bool transfer = true;

        [GameParameters.CustomFloatParameterUI("#LOC_RTPS_settings_breach_share", minValue = 0f, maxValue = 1f, stepCount = 21, displayFormat = "N0", asPercentage = true, toolTip = "#LOC_RTPS_settings_breach_share_tip")]
        public float breachShare = (float)ShieldSolver.BreachShare;

        [GameParameters.CustomIntParameterUI("#LOC_RTPS_settings_variation", minValue = 0, maxValue = 10, stepSize = 1, toolTip = "#LOC_RTPS_settings_variation_tip")]
        public int variationPercent = 2;

        [GameParameters.CustomParameterUI("#LOC_RTPS_settings_warnings", toolTip = "#LOC_RTPS_settings_warnings_tip")]
        public bool warnings = true;

        /// <summary>Saved with the other fields (public); not shown.</summary>
        public int presetVersion = CurrentPresetVersion;

        private DifficultyPreset shown = (DifficultyPreset)(-1);
        private static readonly RtpsDifficulty Fallback = new RtpsDifficulty();
        private static readonly ShieldSettings Settings = new ShieldSettings();
        private static int settingsFrame = -1;

        internal static RtpsDifficulty Current
        {
            get
            {
                if (HighLogic.CurrentGame == null || HighLogic.CurrentGame.Parameters == null)
                {
                    return Fallback;
                }
                RtpsDifficulty value = HighLogic.CurrentGame.Parameters.CustomParams<RtpsDifficulty>();
                return value ?? Fallback;
            }
        }

        /// <summary>The settings of the current save as the solver reads them. Shared; refreshed once per frame.</summary>
        internal static ShieldSettings CurrentSettings
        {
            get
            {
                if (settingsFrame != Time.frameCount)
                {
                    settingsFrame = Time.frameCount;
                    Current.CopyTo(Settings);
                }
                return Settings;
            }
        }

        /// <summary>The preset in use, Custom included; a value outside the list reads as Realistic.</summary>
        internal DifficultyPreset Preset
        {
            get { return Enum.IsDefined(typeof(DifficultyPreset), preset) ? preset : DifficultyPreset.Realistic; }
        }

        internal void CopyTo(ShieldSettings target)
        {
            DifficultyPreset active = Preset;
            if (DifficultyPresets.IsNamed(active))
            {
                // Straight from the table: no slider rounding, and a patched preset reaches existing saves.
                target.CopyFrom(RtpsCatalogue.Difficulty.Values(active));
            }
            else
            {
                target.Damage = damage;
                target.Severity = severity;
                target.SurfaceDamageTime = surfaceDamageTime;
                target.BondDamageTime = bondDamageTime;
                target.StructureDamageTime = structureDamageTime;
                target.Failure = failure;
                target.Transfer = transfer;
                target.BreachShare = breachShare;
                target.CellVariation = variationPercent / 100.0;
            }
            DifficultyPresets.Clamp(target);
        }

        /// <summary>Shows the values of a named preset on the sliders (they are not editable there).</summary>
        private void Mirror(DifficultyPreset value)
        {
            if (DifficultyPresets.IsNamed(value))
            {
                ShieldSettings s = RtpsCatalogue.Difficulty.Values(value);
                damage = s.Damage;
                severity = (float)s.Severity;
                surfaceDamageTime = (float)s.SurfaceDamageTime;
                bondDamageTime = (float)s.BondDamageTime;
                structureDamageTime = (float)s.StructureDamageTime;
                failure = s.Failure;
                transfer = s.Transfer;
                breachShare = (float)s.BreachShare;
                variationPercent = Mathf.Clamp(Mathf.RoundToInt((float)(s.CellVariation * 100.0)), 0, 10);
            }
            shown = value;
        }

        public override void SetDifficultyPreset(GameParameters.Preset gamePreset)
        {
            DifficultyPresets table = RtpsCatalogue.Difficulty;
            switch (gamePreset)
            {
                case GameParameters.Preset.Easy:
                    preset = table.ForLevel(0);
                    break;
                case GameParameters.Preset.Moderate:
                    preset = table.ForLevel(2);
                    break;
                case GameParameters.Preset.Hard:
                    preset = table.ForLevel(3);
                    break;
                case GameParameters.Preset.Custom:
                    preset = DifficultyPreset.Custom;
                    break;
                default:
                    preset = table.ForLevel(1);
                    break;
            }
            Mirror(preset);
        }

        public override void OnLoad(ConfigNode node)
        {
            // KSP's ParameterNode.Load parses the fields first and calls OnLoad afterwards (KSP 1.12.5, read from
            // Assembly-CSharp). A name the enum no longer has (StockRealistic ...) fails its Enum.Parse, which KSP
            // catches and logs ("Couldn't parse value ..."), leaving the field at its default; the saved text is
            // read again here and mapped to today's level.
            base.OnLoad(node);
            DifficultyPreset parsed;
            string migrated = DifficultyPresets.MigrateSavedName(node != null ? node.GetValue("preset") : null);
            if (migrated != null && TryParsePreset(migrated, out parsed))
            {
                preset = parsed;
            }
            if (!Enum.IsDefined(typeof(DifficultyPreset), preset))
            {
                preset = DifficultyPreset.Realistic;
            }
            presetVersion = CurrentPresetVersion;
            Mirror(preset);
        }

        private static bool TryParsePreset(string text, out DifficultyPreset value)
        {
            if (DifficultyPresets.TryParseNamed(text, out value))
            {
                return true;
            }
            if (string.Equals(text, DifficultyPreset.Custom.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                value = DifficultyPreset.Custom;
                return true;
            }
            return false;
        }

        public override IList ValidValues(MemberInfo member)
        {
            return member.Name == "preset" ? Enum.GetValues(typeof(DifficultyPreset)) : null;
        }

        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            if (preset != shown)
            {
                Mirror(Preset);
            }
            return true;
        }

        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            // The individual values are editable only in Custom; a named preset shows its own.
            if (member.Name == "preset" || member.Name == "warnings")
            {
                return true;
            }
            if (Preset != DifficultyPreset.Custom)
            {
                return false;
            }
            if (member.Name == "severity")
            {
                // It scales the structure as well as the cells.
                return damage || failure;
            }
            if (member.Name == "surfaceDamageTime" || member.Name == "bondDamageTime")
            {
                return damage;
            }
            if (member.Name == "structureDamageTime")
            {
                return failure;
            }
            if (member.Name == "breachShare")
            {
                return transfer;
            }
            return true;
        }

        /// <summary>Localized name of a preset, for the window.</summary>
        internal static string DisplayName(DifficultyPreset value)
        {
            switch (value)
            {
                case DifficultyPreset.Friendly: return Localizer.Format("#LOC_RTPS_preset_friendly");
                case DifficultyPreset.Realistic: return Localizer.Format("#LOC_RTPS_preset_realistic");
                case DifficultyPreset.Strict: return Localizer.Format("#LOC_RTPS_preset_strict");
                default: return Localizer.Format("#LOC_RTPS_preset_custom");
            }
        }
    }
}
