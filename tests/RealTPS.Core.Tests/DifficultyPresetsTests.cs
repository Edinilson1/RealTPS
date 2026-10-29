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
    }
}
