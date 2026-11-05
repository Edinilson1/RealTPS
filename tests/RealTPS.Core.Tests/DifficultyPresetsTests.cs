using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealTPS.Core.Cfg;
using RealTPS.Core.Materials;
using RealTPS.Core.Shield;
using Xunit;

namespace RealTPS.Core.Tests
{
    public class DifficultyPresetsTests
    {
        private static DifficultyPresets Parse(params string[] cfg)
        {
            var nodes = new List<CfgNode>();
            foreach (string text in cfg)
            {
                nodes.AddRange(CfgParser.Parse(text).GetNodes(DifficultyPresets.NodeName));
            }
            return DifficultyPresets.Load(nodes);
        }

        // ----- the presets really differ -----

        [Fact]
        public void ThereAreFourPresetsAndEveryNamedOneHasValuesOfItsOwn()
        {
            Assert.Equal(4, Enum.GetValues(typeof(DifficultyPreset)).Length);
            Assert.Equal(3, DifficultyPresets.Named.Length);
            Assert.False(DifficultyPresets.IsNamed(DifficultyPreset.Custom));
            DifficultyPresets presets = DifficultyPresets.BuiltIn();
            foreach (DifficultyPreset a in DifficultyPresets.Named)
            {
                foreach (DifficultyPreset b in DifficultyPresets.Named)
                {
                    if (a != b)
                    {
                        Assert.False(presets.Values(a).SameAs(presets.Values(b)), a + " and " + b + " have the same values");
                    }
                }
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => presets.Values(DifficultyPreset.Custom));
        }

        [Fact]
        public void RealisticIsTheCalibratedModel()
        {
            Assert.True(DifficultyPresets.BuiltIn().Values(DifficultyPreset.Realistic).SameAs(new ShieldSettings()));
        }

        [Fact]
        public void LevelsOrderFromFriendlyToStrictWithTheSameTimeConstants()
        {
            DifficultyPresets p = DifficultyPresets.BuiltIn();
            DifficultyPreset[] levels = { DifficultyPreset.Friendly, DifficultyPreset.Realistic, DifficultyPreset.Strict };
            for (int i = 1; i < 3; i++)
            {
                ShieldSettings softer = p.Values(levels[i - 1]), harder = p.Values(levels[i]);
                Assert.True(harder.Severity > softer.Severity);
                Assert.True(harder.BreachShare > softer.BreachShare);
                Assert.True(harder.CellVariation > softer.CellVariation);
            }
            foreach (DifficultyPreset level in levels)
            {
                ShieldSettings s = p.Values(level);
                Assert.Equal(ShieldSolver.SurfaceDamageTime, s.SurfaceDamageTime);
                Assert.Equal(ShieldSolver.BondDamageTime, s.BondDamageTime);
                Assert.Equal(ShieldSolver.StructureDamageTime, s.StructureDamageTime);
            }
        }

        [Fact]
        public void OldSavesLoadAsTheLevelOfTheSameName()
        {
            // presetVersion 2 had a stock family and a real-scale family; both become the plain level.
            Assert.Equal("Friendly", DifficultyPresets.MigrateSavedName("StockFriendly"));
            Assert.Equal("Realistic", DifficultyPresets.MigrateSavedName("StockRealistic"));
            Assert.Equal("Strict", DifficultyPresets.MigrateSavedName(" stockstrict "));
            Assert.Equal("Friendly", DifficultyPresets.MigrateSavedName("Friendly"));
            Assert.Equal("Realistic", DifficultyPresets.MigrateSavedName("Realistic"));
            Assert.Equal("Strict", DifficultyPresets.MigrateSavedName("Strict"));
            Assert.Equal("Custom", DifficultyPresets.MigrateSavedName("Custom"));
            // Anything else is left alone, for the caller to reject.
            Assert.Equal("StockCustom", DifficultyPresets.MigrateSavedName("StockCustom"));
            Assert.Equal("Stock", DifficultyPresets.MigrateSavedName("Stock"));
            Assert.Equal("Hardcore", DifficultyPresets.MigrateSavedName("Hardcore"));
            Assert.Null(DifficultyPresets.MigrateSavedName(null));
            foreach (DifficultyPreset preset in Enum.GetValues(typeof(DifficultyPreset)))
            {
                Assert.Equal(preset.ToString(), DifficultyPresets.MigrateSavedName(preset.ToString()));
            }
        }

        /// <summary>The same over-temperature, held for the same time, under each preset.</summary>
        private static double IntegrityLost(DifficultyPreset preset, double skin, double seconds)
        {
            MaterialDefinition tile = TestMaterials.Tile();
            ShieldState state = ShieldSolverTests.Stocked(tile);
            var result = new ShieldStepResult(state.Count);
            ShieldStepInput input = ShieldSolverTests.Input(skin, 0.05);
            input.Settings = DifficultyPresets.BuiltIn().Values(preset).Clone();
            input.Settings.CellVariation = 0.0;
            for (double t = 0.0; t < seconds; t += 0.05)
            {
                ShieldSolver.Step(tile, state, input, result);
            }
            return 1.0 - state.MeanIntegrity;
        }

        [Fact]
        public void ThePresetsChangeWhatTheSolverDoes()
        {
            double friendly = IntegrityLost(DifficultyPreset.Friendly, 1700.0, 0.25);
            double realistic = IntegrityLost(DifficultyPreset.Realistic, 1700.0, 0.25);
            double strict = IntegrityLost(DifficultyPreset.Strict, 1700.0, 0.25);
            Assert.True(friendly > 0.0);
            Assert.Equal(0.5 * realistic, friendly, 9);
            Assert.Equal(1.5 * realistic, strict, 9);
        }

        [Fact]
        public void TheBreachShareFollowsThePreset()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            var shares = new Dictionary<DifficultyPreset, double>();
            foreach (DifficultyPreset preset in DifficultyPresets.Named)
            {
                ShieldState state = ShieldSolverTests.Stocked(m);
                state.Inventory[10] = 0.0;
                var result = new ShieldStepResult(state.Count);
                ShieldStepInput input = ShieldSolverTests.Input(1500.0, 0.02, 5.0e6, 0.4);
                input.Settings = DifficultyPresets.BuiltIn().Values(preset).Clone();
                ShieldSolver.Step(m, state, input, result);
                shares[preset] = result.BreachTotal / (5.0e6 * state.Layout.AreaFraction(10) * result.Weights[10] * 0.02);
            }
            Assert.Equal(0.25, shares[DifficultyPreset.Friendly], 9);
            Assert.Equal(0.50, shares[DifficultyPreset.Realistic], 9);
            Assert.Equal(0.75, shares[DifficultyPreset.Strict], 9);
        }

        [Fact]
        public void TheDamageLawsTakeTheTimeConstantOfTheDifficulty()
        {
            MaterialDefinition tile = TestMaterials.Tile();
            double skin = 0.5 * (tile.DamageSurfaceTemp + tile.MaxSurfaceTemp);
            double calibrated = ShieldSolver.SurfaceRate(tile, skin, 1.0, 1.0);
            Assert.True(calibrated > 0.0);
            Assert.Equal(calibrated / 3.0, ShieldSolver.SurfaceRate(tile, skin, 1.0, 1.0, 3.0 * ShieldSolver.SurfaceDamageTime), 12);
            Assert.Equal(0.0, ShieldSolver.SurfaceRate(tile, skin, 1.0, 1.0, 0.0));
            double bond = ShieldSolver.BondRate(560.0, 580.0);
            Assert.True(bond > 0.0);
            Assert.Equal(bond / 3.0, ShieldSolver.BondRate(560.0, 580.0, 3.0 * ShieldSolver.BondDamageTime), 12);
            Assert.Equal(0.0, ShieldSolver.BondRate(560.0, 580.0, double.NaN));
        }

        [Fact]
        public void BadSettingsFallBackToTheCalibratedValuesInTheSolver()
        {
            MaterialDefinition tile = TestMaterials.Tile();
            ShieldState reference = ShieldSolverTests.Stocked(tile);
            ShieldState bad = ShieldSolverTests.Stocked(tile);
            var result = new ShieldStepResult(reference.Count);
            ShieldStepInput input = ShieldSolverTests.Input(1700.0);
            ShieldSolver.Step(tile, reference, input, result);
            input.Settings = new ShieldSettings { CellVariation = 0.0, SurfaceDamageTime = double.NaN, BondDamageTime = -4.0, StructureDamageTime = 0.0 };
            ShieldSolver.Step(tile, bad, input, result);
            Assert.Equal(reference.MeanIntegrity, bad.MeanIntegrity, 12);
        }

        // ----- the cfg -----

        [Fact]
        public void ANodeOverridesOnlyTheKeysItStates()
        {
            DifficultyPresets p = Parse("RTPS_DIFFICULTY { PRESET { name = Realistic\n bondDamageTime = 240 } }");
            Assert.Empty(p.Issues);
            ShieldSettings expected = DifficultyPresets.BuiltInValues(DifficultyPreset.Realistic);
            expected.BondDamageTime = 240.0;
            Assert.True(p.Values(DifficultyPreset.Realistic).SameAs(expected));
            Assert.True(p.Values(DifficultyPreset.Strict).SameAs(DifficultyPresets.BuiltInValues(DifficultyPreset.Strict)));
        }

        [Fact]
        public void ALaterNodeWinsKeyByKey()
        {
            DifficultyPresets p = Parse(
                "RTPS_DIFFICULTY { hard = Realistic\n PRESET { name = Strict\n damageRate = 2\n breachShare = 0.9 } }",
                "RTPS_DIFFICULTY { hard = strict\n PRESET { name = strict\n damageRate = 2.5 } }");
            Assert.Empty(p.Issues);
            Assert.Equal(DifficultyPreset.Strict, p.Hard);
            Assert.Equal(2.5, p.Values(DifficultyPreset.Strict).Severity);
            Assert.Equal(0.9, p.Values(DifficultyPreset.Strict).BreachShare);
            Assert.Equal(DifficultyPreset.Friendly, p.ForLevel(0));
            Assert.Equal(DifficultyPreset.Realistic, p.ForLevel(1));
            Assert.Equal(DifficultyPreset.Realistic, p.ForLevel(2));
            Assert.Equal(DifficultyPreset.Strict, p.ForLevel(3));
        }

        [Fact]
        public void BadEntriesAreReportedAndNeverLoaded()
        {
            DifficultyPresets p = Parse(
                "RTPS_DIFFICULTY\n{\n easy = Custom\n normal = Hardcore\n hard = StockStrict\n colour = red\n" +
                " PRESET { name = Custom\n damageRate = 2 }\n" +
                " PRESET { damageRate = 2 }\n" +
                " PRESET { name = StockRealistic\n damageRate = 2 }\n" +
                " PRESET { name = Friendly\n damageRate = 9\n bondDamageTime = 1\n breachShare = -1\n cellVariation = 0.5\n surfaceDamageTime = fast\n failure = maybe\n speed = 3 }\n" +
                " OTHER { }\n}");
            Assert.All(p.Issues, i => Assert.False(i.IsError));
            Assert.Equal(15, p.Issues.Count);
            Assert.Equal(DifficultyPreset.Friendly, p.Easy);
            Assert.Equal(DifficultyPreset.Realistic, p.Normal);
            Assert.Equal(DifficultyPreset.Strict, p.Hard);
            Assert.True(p.Values(DifficultyPreset.Realistic).SameAs(DifficultyPresets.BuiltInValues(DifficultyPreset.Realistic)));
            ShieldSettings friendly = p.Values(DifficultyPreset.Friendly);
            Assert.Equal(DifficultyPresets.MaxDamageRate, friendly.Severity);
            Assert.Equal(DifficultyPresets.MinBondDamageTime, friendly.BondDamageTime);
            Assert.Equal(0.0, friendly.BreachShare);
            Assert.Equal(DifficultyPresets.MaxCellVariation, friendly.CellVariation);
            Assert.Equal(DifficultyPresets.BuiltInValues(DifficultyPreset.Friendly).SurfaceDamageTime, friendly.SurfaceDamageTime);
            Assert.True(friendly.Failure);
        }

        [Fact]
        public void ClampKeepsEveryValueInItsRange()
        {
            var s = new ShieldSettings
            {
                Severity = 99.0,
                SurfaceDamageTime = double.NaN,
                BondDamageTime = 0.0,
                StructureDamageTime = 1000.0,
                BreachShare = 2.0,
                CellVariation = -1.0
            };
            DifficultyPresets.Clamp(s);
            Assert.Equal(DifficultyPresets.MaxDamageRate, s.Severity);
            Assert.Equal(ShieldSolver.SurfaceDamageTime, s.SurfaceDamageTime);
            Assert.Equal(DifficultyPresets.MinBondDamageTime, s.BondDamageTime);
            Assert.Equal(DifficultyPresets.MaxStructureDamageTime, s.StructureDamageTime);
            Assert.Equal(1.0, s.BreachShare);
            Assert.Equal(0.0, s.CellVariation);
            foreach (DifficultyPreset preset in DifficultyPresets.Named)
            {
                ShieldSettings copy = DifficultyPresets.BuiltInValues(preset);
                DifficultyPresets.Clamp(copy);
                Assert.True(copy.SameAs(DifficultyPresets.BuiltInValues(preset)), preset + " is outside the ranges");
            }
        }
    }
}
