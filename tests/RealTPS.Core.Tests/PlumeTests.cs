using System;
using System.Collections.Generic;
using System.Linq;
using RealTPS.Core.Materials;
using RealTPS.Core.Shield;
using Xunit;

namespace RealTPS.Core.Tests
{
    /// <summary>Engine jets on a shield: footprint weights and their effect on the step.</summary>
    public class PlumeTests
    {
        private static readonly ShieldLayout Layout = new ShieldLayout(8, 2, 2.0);

        private static double Mean(double[] weights, ShieldLayout layout)
        {
            double sum = 0.0;
            for (int i = 0; i < layout.Count; i++)
            {
                sum += weights[i] * layout.AreaFraction(i);
            }
            return sum;
        }

        private static int Argmax(double[] values, int count)
        {
            int best = 0;
            for (int i = 1; i < count; i++)
            {
                if (values[i] > values[best])
                {
                    best = i;
                }
            }
            return best;
        }

        private static PlumeHit At(int cell, double strength = 1.0, double spread = 0.15)
        {
            double r = Layout.SampleRadius(cell), a = Layout.Angle(cell);
            return new PlumeHit { X = r * Math.Cos(a), Y = r * Math.Sin(a), Strength = strength, Spread = spread };
        }

        [Fact]
        public void FootprintPeaksAtTheHitCellAndKeepsTheMean()
        {
            var w = new double[Layout.Count];
            int target = 2 * Layout.Sectors + 3;     // an outer-band cell
            Assert.True(ShieldWeights.Plume(Layout, new[] { At(target) }, w));
            Assert.Equal(target, Argmax(w, Layout.Count));
            Assert.Equal(1.0, Mean(w, Layout), 9);
            // The opposite side of the face gets next to nothing.
            int opposite = 2 * Layout.Sectors + (3 + Layout.Sectors / 2) % Layout.Sectors;
            Assert.True(w[opposite] < 1.0e-3 * w[target]);
        }

        [Fact]
        public void WideFootprintsSpreadAlmostEvenly()
        {
            var w = new double[Layout.Count];
            ShieldWeights.Plume(Layout, new[] { At(5, spread: 4.0) }, w);
            Assert.InRange(w.Take(Layout.Count).Min(), 0.85, 1.0);
            Assert.InRange(w.Take(Layout.Count).Max(), 1.0, 1.1);
        }

        [Fact]
        public void HitsAddByStrength()
        {
            var w = new double[Layout.Count];
            int a = 2 * Layout.Sectors, b = 2 * Layout.Sectors + Layout.Sectors / 2;
            ShieldWeights.Plume(Layout, new[] { At(a, 3.0), At(b, 1.0) }, w);
            Assert.InRange(w[a] / w[b], 2.9, 3.1);
        }

        [Fact]
        public void NoUsableHitGivesEvenWeights()
        {
            var w = new double[Layout.Count];
            Assert.False(ShieldWeights.Plume(Layout, new List<PlumeHit>(), w));
            Assert.All(w.Take(Layout.Count), v => Assert.Equal(1.0, v));
            Assert.False(ShieldWeights.Plume(Layout, new[] { new PlumeHit { X = double.NaN, Y = 0, Strength = 1, Spread = 0.2 }, new PlumeHit { X = 0, Y = 0, Strength = 0, Spread = 0.2 } }, w));
            Assert.All(w.Take(Layout.Count), v => Assert.Equal(1.0, v));
        }

        [Fact]
        public void SpreadGrowsWithDistanceAndShrinksWithTheFace()
        {
            Assert.True(ShieldWeights.PlumeSpread(10.0, 1.25) > ShieldWeights.PlumeSpread(1.0, 1.25));
            Assert.True(ShieldWeights.PlumeSpread(2.0, 0.3) > ShieldWeights.PlumeSpread(2.0, 1.9));
            Assert.Equal(ShieldWeights.MinPlumeSpread, ShieldWeights.PlumeSpread(0.0, 100.0), 12);
        }

        // ----- the step -----

        private static double[] Consumed(ShieldStepInput input)
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = ShieldSolverTests.Stocked(m, Layout);
            double[] before = (double[])state.Inventory.Clone();
            ShieldSolver.Step(m, state, input, new ShieldStepResult(state.Count));
            return before.Select((v, i) => (v - state.Inventory[i]) / Layout.AreaFraction(i)).ToArray();
        }

        [Fact]
        public void AJetOnOneSideConsumesThatSideFirst()
        {
            int target = 2 * Layout.Sectors + 1;
            var jet = new double[Layout.Count];
            ShieldWeights.Plume(Layout, new[] { At(target) }, jet);
            ShieldStepInput input = ShieldSolverTests.Input(1400.0);
            input.Flow = new Incidence();                 // vacuum: no airflow
            input.PlumePower = 5.0e6;                     // far above what the face radiates: full modulation
            input.PlumeWeights = jet;
            double[] perArea = Consumed(input);
            Assert.Equal(target, Argmax(perArea, Layout.Count));
            int opposite = 2 * Layout.Sectors + (1 + Layout.Sectors / 2) % Layout.Sectors;
            Assert.True(perArea[opposite] < 0.01 * perArea[target]);
        }

        [Fact]
        public void WithoutJetsTheStepIsUnchanged()
        {
            ShieldStepInput plain = ShieldSolverTests.Input(1400.0, power: 2.0e5, tilt: 0.3);
            ShieldStepInput withNull = plain;
            withNull.PlumeWeights = new double[Layout.Count].Select(_ => 5.0).ToArray();   // ignored without power
            Assert.Equal(Consumed(plain), Consumed(withNull));
        }

        [Fact]
        public void UntracedJetHeatDilutesTheAirflowPatternEvenly()
        {
            ShieldStepInput air = ShieldSolverTests.Input(1400.0, power: 1.0e7, tilt: 0.6);
            ShieldStepInput both = air;
            both.PlumePower = 1.0e7;                      // as much again, from an unknown direction
            double[] a = Consumed(air), b = Consumed(both);
            double spreadA = a.Max() / a.Min(), spreadB = b.Max() / b.Min();
            Assert.True(spreadB < spreadA);
            Assert.True(spreadB > 1.0);
        }

        [Fact]
        public void WeakJetsBarelyModulate()
        {
            int target = 2 * Layout.Sectors + 1;
            var jet = new double[Layout.Count];
            ShieldWeights.Plume(Layout, new[] { At(target) }, jet);
            ShieldStepInput input = ShieldSolverTests.Input(1400.0);
            input.Flow = new Incidence();
            input.PlumePower = 1.0;                       // 1 W against hundreds of kW radiated
            input.PlumeWeights = jet;
            var result = new ShieldStepResult(Layout.Count);
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldSolver.Step(m, ShieldSolverTests.Stocked(m, Layout), input, result);
            Assert.True(result.PlumeGate < 1.0e-4);
            Assert.All(result.Weights.Take(Layout.Count), v => Assert.InRange(v, 0.99, 1.01));
        }

        [Fact]
        public void AJetPassesHeatThroughTheHoleItBurnt()
        {
            // On the pad: no airflow, a jet aimed at one cell that has burnt through, another hole far from it.
            int target = 2 * Layout.Sectors + 1;
            int opposite = 2 * Layout.Sectors + (1 + Layout.Sectors / 2) % Layout.Sectors;
            var jet = new double[Layout.Count];
            ShieldWeights.Plume(Layout, new[] { At(target) }, jet);
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = ShieldSolverTests.Stocked(m, Layout);
            state.Inventory[target] = 0.0;
            state.Inventory[opposite] = 0.0;
            ShieldStepInput input = ShieldSolverTests.Input(1400.0);
            input.Flow = new Incidence();
            input.PlumePower = 5.0e6;
            input.PlumeWeights = jet;
            var result = new ShieldStepResult(Layout.Count);
            ShieldSolver.Step(m, state, input, result);

            double expected = ShieldSolver.BreachShare * 5.0e6 * Layout.AreaFraction(target) * result.PlumeWeights[target] * input.Dt;
            Assert.True(expected > 0.0);
            Assert.Equal(expected, result.BreachEnergy[target], 6);
            Assert.True(result.BreachEnergy[opposite] < 0.01 * result.BreachEnergy[target]);
            Assert.Equal(result.BreachTotal, result.BreachFromJets, 9);
        }

        [Fact]
        public void AirAndJetsEachPassOnTheirOwnShare()
        {
            int target = 3;
            var jet = new double[Layout.Count];
            ShieldWeights.Plume(Layout, new[] { At(target) }, jet);
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = ShieldSolverTests.Stocked(m, Layout);
            state.Inventory[target] = 0.0;
            ShieldStepInput input = ShieldSolverTests.Input(1400.0, power: 3.0e5, tilt: 0.5);
            input.PlumePower = 2.0e5;
            input.PlumeWeights = jet;
            var result = new ShieldStepResult(Layout.Count);
            ShieldSolver.Step(m, state, input, result);

            double share = ShieldSolver.BreachShare * Layout.AreaFraction(target) * input.Dt;
            double air = share * 3.0e5 * result.FlowWeights[target];
            double jets = share * 2.0e5 * result.PlumeWeights[target];
            Assert.Equal(air + jets, result.BreachTotal, 6);
            Assert.Equal(jets, result.BreachFromJets, 6);

            // A face in the lee gets nothing from the air, but a jet that reaches it still passes heat through.
            input.Flow = new Incidence { Defined = true, Facing = -0.5, Tilt = 2.0, Azimuth = 0.0 };
            ShieldSolver.Step(m, ShieldSolverTests.Stocked(m, Layout, 1.0), input, result);
            Assert.Equal(0.0, result.BreachTotal);                  // nothing exposed in a fresh shield
            ShieldState holed = ShieldSolverTests.Stocked(m, Layout);
            holed.Inventory[target] = 0.0;
            ShieldSolver.Step(m, holed, input, result);
            Assert.True(result.BreachTotal > 0.0);
            Assert.Equal(result.BreachTotal, result.BreachFromJets, 9);
        }

        [Fact]
        public void BlendedWeightsKeepTheMean()
        {
            int target = 3;
            var jet = new double[Layout.Count];
            ShieldWeights.Plume(Layout, new[] { At(target) }, jet);
            ShieldStepInput input = ShieldSolverTests.Input(1400.0, power: 3.0e5, tilt: 0.5);
            input.PlumePower = 2.0e5;
            input.PlumeWeights = jet;
            var result = new ShieldStepResult(Layout.Count);
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldSolver.Step(m, ShieldSolverTests.Stocked(m, Layout), input, result);
            Assert.Equal(1.0, Mean(result.Weights, Layout), 9);
            Assert.Equal(1.0, Mean(result.FlowWeights, Layout), 9);
        }
    }
}
