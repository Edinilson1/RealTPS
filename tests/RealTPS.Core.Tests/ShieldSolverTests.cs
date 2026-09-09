using System;
using RealTPS.Core.Materials;
using RealTPS.Core.Physics;
using RealTPS.Core.Shield;
using Xunit;

namespace RealTPS.Core.Tests
{
    public class ShieldWeightsTests
    {
        private static readonly double[] Normal = { 0.0, -1.0, 0.0 };
        private static readonly double[] Tangent = { 1.0, 0.0, 0.0 };

        [Theory]
        [InlineData(8, 2, 2.0, 0.0, 0.0)]
        [InlineData(8, 2, 2.0, 0.6, 1.3)]
        [InlineData(12, 3, 0.0, 1.0, -2.0)]
        [InlineData(40, 6, 5.0, 0.3, 4.0)]
        [InlineData(4, 0, 100.0, 0.9, 0.5)]
        public void WeightsOnlyDistribute(int sectors, int rings, double centre, double tilt, double azimuth)
        {
            var layout = new ShieldLayout(sectors, rings, centre);
            var heat = new double[layout.Count];
            var wind = new double[layout.Count];
            ShieldWeights.Compute(layout, tilt, azimuth, heat, wind);
            double heatMean = 0.0, windMean = 0.0;
            for (int i = 0; i < layout.Count; i++)
            {
                Assert.True(heat[i] > 0.0 && wind[i] > 0.0);
                heatMean += heat[i] * layout.AreaFraction(i);
                windMean += wind[i] * layout.AreaFraction(i);
            }
            Assert.Equal(1.0, heatMean, 12);
            Assert.Equal(1.0, windMean, 12);
        }

        [Fact]
        public void FaceOnTheProfileIsRadialAndTheWindwardFactorIsUniform()
        {
            var layout = new ShieldLayout(8, 2, 2.0);
            var heat = new double[layout.Count];
            var wind = new double[layout.Count];
            ShieldWeights.Compute(layout, 0.0, 0.0, heat, wind);
            for (int i = 0; i < layout.Count; i++)
            {
                Assert.Equal(1.0, wind[i], 12);
                // Same band, same weight.
                if (!layout.IsCenter(i))
                {
                    Assert.Equal(heat[layout.Band(i) * layout.Sectors], heat[i], 12);
                }
            }
            Assert.True(heat[layout.CenterIndex] > heat[0]);
            Assert.True(heat[0] > heat[8]);
            Assert.True(heat[8] > heat[16]);
        }

        [Fact]
        public void TiltLoadsTheSideThatLeadsIntoTheFlow()
        {
            var layout = new ShieldLayout(8, 2, 2.0);
            var heat = new double[layout.Count];
            var wind = new double[layout.Count];
            // Leading side at sector 2 (a quarter turn from sector zero).
            ShieldWeights.Compute(layout, 0.5, Math.PI / 2.0, heat, wind);
            int outer = 2 * layout.Sectors;
            Assert.True(heat[outer + 2] > heat[outer + 0]);
            Assert.True(heat[outer + 0] > heat[outer + 6]);
            Assert.Equal(heat[outer + 0], heat[outer + 4], 12);     // symmetric about the flow plane
            // Turning the flow by one sector turns the pattern by one sector.
            var turned = new double[layout.Count];
            ShieldWeights.Compute(layout, 0.5, Math.PI / 2.0 + 2.0 * Math.PI / 8.0, turned, wind);
            for (int s = 0; s < 8; s++)
            {
                Assert.Equal(heat[outer + s], turned[outer + (s + 1) % 8], 12);
            }
        }

        [Fact]
        public void IncidenceFollowsTheFaceFrame()
        {
            FaceFrame frame;
            Assert.True(ShieldGeometry.TryMakeFrame(Normal, Tangent, out frame));
            // normal x tangent = (0,-1,0) x (1,0,0) = (0,0,1)
            Assert.Equal(0.0, frame.Bx, 12);
            Assert.Equal(0.0, frame.By, 12);
            Assert.Equal(1.0, frame.Bz, 12);

            Incidence faceOn = ShieldGeometry.Incidence(frame, 0.0, -250.0, 0.0);
            Assert.True(faceOn.Defined);
            Assert.Equal(1.0, faceOn.Facing, 12);
            Assert.Equal(0.0, faceOn.Tilt, 12);

            Incidence leading = ShieldGeometry.Incidence(frame, 1.0, -1.0, 0.0);
            Assert.Equal(Math.Sqrt(0.5), leading.Facing, 12);
            Assert.Equal(Math.Sqrt(0.5), leading.Tilt, 12);
            Assert.Equal(0.0, leading.Azimuth, 12);                 // the +tangent side leads

            Incidence quarter = ShieldGeometry.Incidence(frame, 0.0, -1.0, 1.0);
            Assert.Equal(Math.PI / 2.0, quarter.Azimuth, 12);       // the +bitangent side leads

            Incidence lee = ShieldGeometry.Incidence(frame, 0.0, 3.0, 0.0);
            Assert.Equal(-1.0, lee.Facing, 12);

            Assert.False(ShieldGeometry.Incidence(frame, 0.0, 0.0, 0.0).Defined);
            Assert.False(ShieldGeometry.Incidence(frame, double.NaN, 0.0, 0.0).Defined);
        }

        [Fact]
        public void FramesAndVectorsAreValidated()
        {
            double[] v;
            Assert.True(ShieldGeometry.TryParseVector("0, -1, 0", out v));
            Assert.Equal(new[] { 0.0, -1.0, 0.0 }, v);
            Assert.Equal("0, -1, 0", ShieldGeometry.FormatVector(v));
            Assert.False(ShieldGeometry.TryParseVector("0, -1", out v));
            Assert.False(ShieldGeometry.TryParseVector("a, b, c", out v));
            Assert.False(ShieldGeometry.TryParseVector("0, NaN, 0", out v));
            Assert.False(ShieldGeometry.TryParseVector(null, out v));

            Assert.True(ShieldGeometry.IsUsableFrame(new[] { 0.0, -2.0, 0.0 }, new[] { 1.0, 5.0, 0.0 }));   // tangent is made perpendicular
            Assert.False(ShieldGeometry.IsUsableFrame(new[] { 0.0, 0.0, 0.0 }, Tangent));
            Assert.False(ShieldGeometry.IsUsableFrame(Normal, new[] { 0.0, 3.0, 0.0 }));                    // parallel
            Assert.False(ShieldGeometry.IsUsableFrame(Normal, null));
        }
    }

    public class ShieldSolverTests
    {
        private const double Area = 2.0;

        internal static ShieldState Stocked(MaterialDefinition m, ShieldLayout layout = null, double fill = 1.0, double area = Area)
        {
            layout = layout ?? new ShieldLayout(8, 2, 2.0);
            double capacity = m.ConsumableArealDensity * area;
            var construction = new ShieldConstruction { Area = area, LayerThickness = m.DesignThickness };
            return ShieldState.Uniform(layout, construction, capacity * fill, capacity, 0.0);
        }

        internal static ShieldStepInput Input(double skin, double dt = 0.02, double power = 0.0, double tilt = 0.0, double capacity = 1.0e9)
        {
            return new ShieldStepInput
            {
                Dt = dt,
                SkinTemp = skin,
                InternalTemp = 300.0,
                ExposedSkinHeatCapacity = capacity,
                DynamicPressure = 0.0,
                OxygenAtmosphere = false,
                ConvectivePower = power,
                Flow = new Incidence { Defined = true, Facing = Math.Sqrt(1.0 - tilt * tilt), Tilt = tilt, Azimuth = 0.7 },
                Settings = new ShieldSettings { CellVariation = 0.0 }
            };
        }

        private static StepInput Lumped(ShieldStepInput input, double remaining)
        {
            return new StepInput
            {
                Dt = input.Dt,
                SkinTemp = input.SkinTemp,
                SkinHeatCapacity = input.ExposedSkinHeatCapacity,
                Area = Area,
                RemainingMass = remaining,
                DynamicPressure = input.DynamicPressure,
                OxygenAtmosphere = input.OxygenAtmosphere
            };
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.4)]
        public void WithEveryCellStockedTheTotalsEqualTheLumpedLaw(double tilt)
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = Stocked(m);
            var result = new ShieldStepResult(state.Count);
            ShieldStepInput input = Input(1500.0, 0.02, 5.0e6, tilt);
            input.DynamicPressure = 100000.0;       // twice the shear limit: every cell is over it
            double before = state.TotalInventory;
            ShieldSolver.Step(m, state, input, result);
            StepResult lumped = AblationSolver.Step(m, Lumped(input, before));

            Assert.True(result.Gate > 0.99);
            Assert.True(lumped.PyrolysedMass > 0.0 && lumped.ShearErodedMass > 0.0);
            Assert.Equal(lumped.PyrolysedMass, result.PyrolysedMass, 12);
            Assert.Equal(lumped.HeatAbsorbed, result.HeatAbsorbed, 6);
            Assert.Equal(lumped.ShearErodedMass, result.ShearErodedMass, 12);
            Assert.Equal(lumped.ShearOverload, result.ShearOverload);
            Assert.Equal(before - lumped.ConsumedMass, state.TotalInventory, 9);
            Assert.Equal(lumped.PyrolysedMass, state.TotalPyrolysed, 12);
            if (tilt > 0.0)
            {
                // ... but not uniformly: the cells that lead into the flow lose more.
                double min = double.MaxValue, max = 0.0;
                for (int i = 0; i < state.Count; i++)
                {
                    min = Math.Min(min, state.Fraction(i));
                    max = Math.Max(max, state.Fraction(i));
                }
                Assert.True(max - min > 1.0e-9);
            }
        }

        [Fact]
        public void ALegacySingleCellIsTheLumpedModelAndNeverFails()
        {
            MaterialDefinition legacy = LegacyProfile.Create(Area, 200.0, 0.4, 3300.0);
            var construction = new ShieldConstruction { Area = Area };
            ShieldState state = ShieldState.Uniform(ShieldLayout.Single(), construction, 150.0, 200.0, 50.0);
            var result = new ShieldStepResult(state.Count);
            ShieldStepInput input = Input(1200.0, 0.02, 4.0e6, 0.5);
            ShieldSolver.Step(legacy, state, input, result);
            StepResult lumped = AblationSolver.Step(legacy, Lumped(input, 150.0));
            Assert.Equal(lumped.PyrolysedMass, result.PyrolysedMass, 12);
            Assert.Equal(lumped.HeatAbsorbed, result.HeatAbsorbed, 6);
            Assert.Equal(0.0, result.Conductance);                  // no layer model: the part keeps its own conduction
            Assert.Equal(3300.0, result.NativeLimit);

            // Exhausted: still no damage, no failure, no leak, its own limit and its own emissivity.
            state.Inventory[0] = 0.0;
            ShieldSolver.Step(legacy, state, input, result);
            Assert.False(result.FailureDue);
            Assert.Equal(1.0, state.Structure);
            Assert.Equal(1.0, state.Integrity[0]);
            Assert.Equal(0.0, result.BreachTotal);
            Assert.Equal(3300.0, result.NativeLimit);
            Assert.Equal(0.4, result.Emissivity, 12);
            Assert.False(state.ThermalInitialised);
        }

        [Fact]
        public void AnEmptyCellNeverDrawsOnAnother()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = Stocked(m);
            ShieldState full = Stocked(m);
            var result = new ShieldStepResult(state.Count);
            var reference = new ShieldStepResult(state.Count);
            const int empty = 18;
            state.Inventory[empty] = 0.0;
            ShieldStepInput input = Input(1500.0, 0.02, 5.0e6, 0.4);
            ShieldSolver.Step(m, full, input, reference);
            ShieldSolver.Step(m, state, input, result);

            double share = state.Layout.AreaFraction(empty) * reference.Weights[empty];
            Assert.Equal(reference.PyrolysedMass * (1.0 - share), result.PyrolysedMass, 12);
            Assert.Equal(0.0, state.Inventory[empty]);
            for (int i = 0; i < state.Count; i++)
            {
                if (i != empty)
                {
                    Assert.Equal(full.Inventory[i], state.Inventory[i], 15);    // the others lose exactly what they would anyway
                }
            }
        }

        [Fact]
        public void ACellConsumesExactlyWhatItHas()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = Stocked(m);
            var result = new ShieldStepResult(state.Count);
            state.Inventory[3] = 1.0e-7;
            ShieldStepInput input = Input(2000.0, 0.5, 5.0e6);
            ShieldSolver.Step(m, state, input, result);
            Assert.Equal(0.0, state.Inventory[3]);
            Assert.Equal(1.0e-7, state.Pyrolysed[3], 15);
            Assert.True(result.ExposedTime[3] > 0.49);              // it emptied almost at once
            Assert.Equal(1, result.NewlyExposed);
            double total = 0.0;
            for (int i = 0; i < state.Count; i++)
            {
                total += state.Pyrolysed[i];
            }
            Assert.Equal(result.PyrolysedMass, total, 12);
            Assert.Equal(result.PyrolysedMass * m.Ablation.HeatOfAblation, result.HeatAbsorbed, 6);
        }

        [Fact]
        public void TheSinkStaysInsideItsBudget()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = Stocked(m);
            var result = new ShieldStepResult(state.Count);
            ShieldStepInput input = Input(1500.0, 0.5, 5.0e6, 0.3, capacity: 50.0);
            double budget = 50.0 * (1500.0 - m.Ablation.OnsetTemp);
            ShieldSolver.Step(m, state, input, result);
            Assert.Equal(budget, result.HeatAbsorbed, 6);
            Assert.Equal(budget / m.Ablation.HeatOfAblation, result.PyrolysedMass, 12);

            // Without a usable capacity the bound is unknown: no sink, no pyrolysis, no leak, no NaN.
            foreach (double capacity in new[] { 0.0, double.NaN, double.PositiveInfinity, -3.0 })
            {
                ShieldState s = Stocked(m);
                s.Inventory[2] = 0.0;
                input.ExposedSkinHeatCapacity = capacity;
                ShieldSolver.Step(m, s, input, result);
                Assert.Equal(0.0, result.HeatAbsorbed);
                Assert.Equal(0.0, result.PyrolysedMass);
                Assert.Equal(0.0, result.BreachTotal);
                AssertFinite(s, result);
            }
        }

        [Fact]
        public void ErosionAndOxidationNeitherCoolNorMakeChar()
        {
            MaterialDefinition carbon = TestMaterials.Carbon();
            ShieldState state = Stocked(carbon);
            var result = new ShieldStepResult(state.Count);
            ShieldStepInput input = Input(1500.0, 0.02, 2.0e6);
            input.OxygenAtmosphere = true;
            ShieldSolver.Step(carbon, state, input, result);
            Assert.True(result.OxidisedMass > 0.0);
            Assert.Equal(0.0, result.HeatAbsorbed);
            Assert.Equal(0.0, result.PyrolysedMass);
            Assert.Equal(0.0, state.TotalPyrolysed);
            Assert.Equal(carbon.Emissivity, result.Emissivity, 12);

            // Shear on an ablator: mass goes, the char fraction and the emissivity do not move.
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState ablator = Stocked(m);
            ShieldStepInput shear = Input(400.0);                   // below onset
            shear.DynamicPressure = 150000.0;
            ShieldSolver.Step(m, ablator, shear, result);
            Assert.True(result.ShearErodedMass > 0.0 && result.ShearOverload);
            Assert.Equal(0.0, ablator.TotalPyrolysed);
            Assert.Equal(m.Emissivity, result.Emissivity, 12);
        }

        [Fact]
        public void InvalidInputsChangeNothing()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            foreach (ShieldStepInput bad in new[]
            {
                Input(1500.0, 0.0), Input(1500.0, -1.0), Input(1500.0, double.NaN), Input(double.NaN), Input(0.0), Input(double.PositiveInfinity)
            })
            {
                ShieldState state = Stocked(m);
                var result = new ShieldStepResult(state.Count);
                ShieldSolver.Step(m, state, bad, result);
                Assert.Equal(state.TotalCapacity, state.TotalInventory);
                Assert.Equal(0.0, result.HeatAbsorbed);
                Assert.False(state.ThermalInitialised);
                Assert.Equal(m.MaxSurfaceTemp, result.NativeLimit);
            }
            ShieldState ok = Stocked(m);
            Assert.Throws<ArgumentNullException>(() => ShieldSolver.Step(null, ok, Input(1500.0), new ShieldStepResult(ok.Count)));
            Assert.Throws<ArgumentNullException>(() => ShieldSolver.Step(m, null, Input(1500.0), new ShieldStepResult(ok.Count)));
            Assert.Throws<ArgumentException>(() => ShieldSolver.Step(m, ok, Input(1500.0), new ShieldStepResult(3)));
        }

        // ----- observer -----

        // ----- damage -----

        // ----- exposure, leak, failure -----

        [Fact]
        public void OutcomesDoNotDependOnTheTimestep()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            double[] inventory = new double[3], breach = new double[3], structure = new double[3], exposed = new double[3], hottest = new double[3];
            double[] steps = { 0.01, 0.04, 0.25 };
            for (int k = 0; k < steps.Length; k++)
            {
                ShieldState state = Stocked(m, null, 0.09);         // thin: the windward cells empty during the run
                for (int i = 0; i < 4; i++)
                {
                    state.Inventory[i] = 0.0;                       // four cells are bare from the start: their substrate loads the structure
                }
                var result = new ShieldStepResult(state.Count);
                double time = 0.0;
                while (time < 50.0 - 1.0e-9)
                {
                    ShieldStepInput input = Input(1600.0, steps[k], 2.0e6, 0.9);
                    input.Settings.Damage = false;
                    ShieldSolver.Step(m, state, input, result);
                    breach[k] += result.BreachTotal;
                    time += steps[k];
                }
                inventory[k] = state.TotalInventory;
                structure[k] = state.Structure;
                exposed[k] = state.ExposedArea(true);
                for (int i = 0; i < state.Count; i++)
                {
                    hottest[k] = Math.Max(hottest[k], state.SubstrateTemp[i]);
                }
            }
            Assert.True(exposed[0] > 0.08 && exposed[0] < 1.0, "the case must cross cell exhaustion, exposed " + exposed[0]);
            Assert.True(structure[0] < 1.0 && structure[0] > 0.0, "the case must load the structure, S " + structure[0]);
            for (int k = 1; k < steps.Length; k++)
            {
                Assert.InRange(inventory[k] / inventory[0], 0.999, 1.001);  // consumption does not depend on the step
                Assert.Equal(exposed[0], exposed[k], 9);
                Assert.InRange(breach[k] / breach[0], 0.97, 1.03);  // cells that empty mid-step leak for the rest of it
                Assert.InRange(hottest[k] / hottest[0], 0.98, 1.02);
                Assert.InRange(structure[k], structure[0] - 0.05, structure[0] + 0.05);
            }
        }

        // ----- conduction into the part -----

        [Fact]
        public void TheConductionMultiplierMapsTheSeriesConductanceOntoTheEngine()
        {
            // Installed constants: 120, heatConductivity 0.12, 0.005, 0.63 -> 226.8 W/(m2 K).
            Assert.Equal(226.8, ShieldSolver.EngineConductance(120.0, 0.12, 0.005, 0.63), 9);
            // Class defaults give the same product.
            Assert.Equal(226.8, ShieldSolver.EngineConductance(20.0, 0.12, 0.03, 0.63), 9);
            // The occlusion rule changes it twelve-fold.
            Assert.Equal(18.9, ShieldSolver.EngineConductance(120.0, 0.01, 0.005, 0.63), 9);

            MaterialDefinition m = TestMaterials.Ablator();         // k 0.2, 50 mm
            ShieldState state = Stocked(m);
            double u = ShieldSolver.Conductance(m, state);
            Assert.Equal(1.0 / (0.05 / 0.2 + 1.0 / 200.0 + 1.0 / 20.0), u, 9);
            Assert.Equal(u / 226.8, ShieldSolver.ConductionMultiplier(u, 2.0, 2.0, 226.8), 12);
            Assert.Equal(u / 18.9, ShieldSolver.ConductionMultiplier(u, 2.0, 2.0, 18.9), 12);
            Assert.Equal(0.5 * u / 226.8, ShieldSolver.ConductionMultiplier(u, 2.0, 4.0, 226.8), 12);
            // The protected area never counts for more than the exposed skin the engine has.
            Assert.Equal(u / 226.8, ShieldSolver.ConductionMultiplier(u, 2.0, 0.5, 226.8), 12);
            // Singular cases: nothing is derived.
            Assert.True(double.IsNaN(ShieldSolver.ConductionMultiplier(0.0, 2.0, 2.0, 226.8)));
            Assert.True(double.IsNaN(ShieldSolver.ConductionMultiplier(u, 2.0, 0.0, 226.8)));
            Assert.True(double.IsNaN(ShieldSolver.ConductionMultiplier(u, 2.0, 2.0, 0.0)));
            Assert.True(double.IsNaN(ShieldSolver.ConductionMultiplier(double.NaN, 2.0, 2.0, 226.8)));
            Assert.Equal(ShieldSolver.MaxConductionMultiplier, ShieldSolver.ConductionMultiplier(1.0e9, 2.0, 2.0, 226.8));
            Assert.Equal(ShieldSolver.MinConductionMultiplier, ShieldSolver.ConductionMultiplier(1.0e-9, 2.0, 2.0, 226.8));

            // A receding layer conducts more; an exposed cell conducts most.
            ShieldState thin = Stocked(m, null, 0.2);
            Assert.True(ShieldSolver.Conductance(m, thin) > u);
            ShieldState holed = Stocked(m);
            holed.Inventory[0] = 0.0;
            Assert.True(ShieldSolver.Conductance(m, holed) > u);
        }

        internal static void AssertFinite(ShieldState state, ShieldStepResult result)
        {
            for (int i = 0; i < state.Count; i++)
            {
                foreach (double v in new[] { state.Inventory[i], state.Pyrolysed[i], state.Integrity[i], state.LayerTemp[i], state.SubstrateTemp[i], state.Received[i], result.BreachEnergy[i], result.Weights[i] })
                {
                    Assert.False(double.IsNaN(v) || double.IsInfinity(v));
                }
            }
            foreach (double v in new[] { state.Structure, result.HeatAbsorbed, result.BreachTotal, result.Emissivity, result.NativeLimit, result.Conductance })
            {
                Assert.False(double.IsNaN(v) || double.IsInfinity(v));
            }
        }
    }
}
