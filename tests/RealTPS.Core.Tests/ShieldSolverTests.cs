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
        public void TheGateIsAnActivityMeasureThatClosesInTheLee()
        {
            // Radiated: 0.8 * 5.67e-8 * 1500^4 * 2 m2 = about 459 kW.
            Assert.Equal(1.0, ShieldWeights.Gate(1.0e6, 0.8, 1500.0, 2.0, 1.0));
            Assert.InRange(ShieldWeights.Gate(2.0e5, 0.8, 1500.0, 2.0, 1.0), 0.40, 0.47);
            Assert.Equal(0.0, ShieldWeights.Gate(0.0, 0.8, 1500.0, 2.0, 1.0));
            Assert.Equal(0.0, ShieldWeights.Gate(-5.0, 0.8, 1500.0, 2.0, 1.0));
            // Positive whole-part heating does not show that the protected face is windward.
            Assert.Equal(0.0, ShieldWeights.Gate(1.0e6, 0.8, 1500.0, 2.0, 0.0));
            Assert.Equal(0.0, ShieldWeights.Gate(1.0e6, 0.8, 1500.0, 2.0, -0.7));
            Assert.Equal(0.5, ShieldWeights.Gate(1.0e6, 0.8, 1500.0, 2.0, 0.1), 12);
            // Guards.
            Assert.Equal(0.0, ShieldWeights.Gate(1.0e6, 0.0, 1500.0, 2.0, 1.0));
            Assert.Equal(0.0, ShieldWeights.Gate(1.0e6, 0.8, double.NaN, 2.0, 1.0));
            Assert.Equal(0.0, ShieldWeights.Gate(double.NaN, 0.8, 1500.0, 2.0, 1.0));
            Assert.Equal(0.0, ShieldWeights.Gate(1.0e6, 0.8, 1500.0, 0.0, 1.0));

            var w = new[] { 0.5, 1.5, 1.0 };
            ShieldWeights.ApplyGate(w, 3, 0.0);
            Assert.Equal(new[] { 1.0, 1.0, 1.0 }, w);
            w = new[] { 0.5, 1.5, 1.0 };
            ShieldWeights.ApplyGate(w, 3, 0.5);
            Assert.Equal(new[] { 0.75, 1.25, 1.0 }, w);
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
        public void TurningTheCraftWithoutHeatingChangesNothing()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState a = Stocked(m);
            ShieldState b = Stocked(m);
            var ra = new ShieldStepResult(a.Count);
            var rb = new ShieldStepResult(b.Count);
            for (int step = 0; step < 200; step++)
            {
                ShieldStepInput ia = Input(1400.0, 0.02, 0.0, 0.0);
                ShieldStepInput ib = Input(1400.0, 0.02, 0.0, 0.9);
                ib.Flow.Azimuth = step * 0.3;                       // tumbling
                ShieldSolver.Step(m, a, ia, ra);
                ShieldSolver.Step(m, b, ib, rb);
            }
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(a.Inventory[i], b.Inventory[i]);
                Assert.Equal(a.SubstrateTemp[i], b.SubstrateTemp[i]);
                Assert.Equal(a.Integrity[i], b.Integrity[i]);
                Assert.Equal(1.0, rb.Weights[i]);
            }
            Assert.True(a.TotalInventory < a.TotalCapacity);        // it is still hot: consumption goes on, uniformly
        }

        [Fact]
        public void NoSpatialModulationWithTheFaceTurnedAway()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = Stocked(m);
            state.Inventory[4] = 0.0;
            var result = new ShieldStepResult(state.Count);
            ShieldStepInput input = Input(1500.0, 0.02, 5.0e6, 0.6);
            input.Flow.Facing = -0.8;
            ShieldSolver.Step(m, state, input, result);
            Assert.Equal(0.0, result.Gate);
            for (int i = 0; i < state.Count; i++)
            {
                Assert.Equal(1.0, result.Weights[i]);
                Assert.Equal(1.0, result.Windward[i]);
            }
            Assert.True(result.PyrolysedMass > 0.0);                // the lumped consumption is untouched
            Assert.Equal(0.0, result.BreachTotal);                  // and nothing leaks through a face in the lee
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

        [Fact]
        public void TheObserverSettlesOnTheSeriesResistance()
        {
            MaterialDefinition m = TestMaterials.Ablator();         // k = 0.2
            var c = new ShieldConstruction { Area = 1.0 };
            double layer = 300.0, substrate = 300.0;
            const double thickness = 0.03;
            for (int i = 0; i < 400000; i++)
            {
                ShieldSolver.ObserveProtected(m, c, thickness, 0.5, 1500.0, 300.0, ref layer, ref substrate);
            }
            double g = 1.0 / (thickness / m.Conductivity + 1.0 / ShieldSolver.ContactConductance);
            double expected = (g * 1500.0 + c.BackConductance * 300.0) / (g + c.BackConductance);
            Assert.Equal(expected, substrate, 6);
            Assert.InRange(layer, substrate, 1500.0);
        }

        [Fact]
        public void TheResistanceIsMonotonicAndContinuousAtExposure()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            var c = new ShieldConstruction { Area = 1.0 };
            double previous = double.MaxValue;
            // Steady substrate temperature rises as the layer recedes and meets the exposed value without a jump.
            foreach (double thickness in new[] { 0.05, 0.01, 0.001, 1.0e-4, 2.0e-5, 1.0e-5, 9.9e-6, 1.0e-6, 1.0e-9, 0.0 })
            {
                double layer = 300.0, substrate = 300.0;
                for (int i = 0; i < 2000; i++)
                {
                    ShieldSolver.ObserveProtected(m, c, thickness, 50.0, 1500.0, 300.0, ref layer, ref substrate);
                }
                Assert.False(double.IsNaN(substrate) || double.IsNaN(layer));
                double resistance = (1500.0 - substrate) / ((substrate - 300.0) * c.BackConductance);
                Assert.Equal(thickness / m.Conductivity + 1.0 / ShieldSolver.ContactConductance, resistance, 9);
                Assert.True(resistance <= previous);
                previous = resistance;
            }
            double exposedLayer = 300.0, exposed = 300.0;
            for (int i = 0; i < 2000; i++)
            {
                ShieldSolver.ObserveExposed(c, 50.0, 1500.0, 300.0, ref exposedLayer, ref exposed);
            }
            Assert.Equal(1.0 / ShieldSolver.ContactConductance, (1500.0 - exposed) / ((exposed - 300.0) * c.BackConductance), 9);
            Assert.Equal(1500.0, exposedLayer);
        }

        [Fact]
        public void ALayerThinnerThanTheNodeCutoffIsStillALayer()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = Stocked(m);
            double cell = state.CellArea(5);
            state.Inventory[5] = m.Density * cell * 5.0e-6;         // 5 micrometres left
            var result = new ShieldStepResult(state.Count);
            ShieldSolver.Step(m, state, Input(500.0), result);      // below onset: nothing is consumed
            Assert.False(state.IsExposed(5, true));
            Assert.Equal(0.0, result.ExposedTime[5]);
            Assert.Equal(1.0, state.Integrity[5]);
            AssertFinite(state, result);
            // Its share of the conductance is the series value with its own thickness, not the exposed one.
            double own = state.Layout.AreaFraction(5) / (5.0e-6 / m.Conductivity + 1.0 / ShieldSolver.ContactConductance + 1.0 / state.Construction.BackConductance);
            double rest = 0.0;
            for (int i = 0; i < state.Count; i++)
            {
                if (i != 5)
                {
                    rest += state.Layout.AreaFraction(i) / (state.Thickness(i, m) / m.Conductivity + 1.0 / ShieldSolver.ContactConductance + 1.0 / state.Construction.BackConductance);
                }
            }
            Assert.Equal(own + rest, result.Conductance, 12);
        }

        [Fact]
        public void TheBondKeepsHeatingAfterThePulse()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = Stocked(m, null, 0.15);             // a thin layer
            var result = new ShieldStepResult(state.Count);
            double peak = 0.0, peakTime = 0.0, time = 0.0;
            const double pulse = 60.0;
            for (int i = 0; i < 20000; i++)
            {
                double skin = time < pulse ? 1400.0 : 300.0 + 1100.0 * Math.Exp(-(time - pulse) / 40.0);
                ShieldStepInput input = Input(skin, 0.05, time < pulse ? 3.0e6 : 0.0);
                input.Settings.Damage = false;
                input.InternalTemp = 300.0;
                ShieldSolver.Step(m, state, input, result);
                time += 0.05;
                if (state.SubstrateTemp[0] > peak)
                {
                    peak = state.SubstrateTemp[0];
                    peakTime = time;
                }
            }
            Assert.True(peakTime > pulse + 5.0, "the substrate should peak after the heat pulse, peaked at " + peakTime);
            Assert.True(peak > 320.0);
        }

        // ----- damage -----

        [Fact]
        public void BondRateIsContinuousBoundedAndIndependentOfAnyNativeLimit()
        {
            const double limit = 523.0;
            Assert.Equal(0.0, ShieldSolver.BondRate(limit, 300.0));
            Assert.Equal(0.0, ShieldSolver.BondRate(limit, limit - ShieldSolver.BondRampStart));
            Assert.True(ShieldSolver.BondRate(limit, limit - ShieldSolver.BondRampStart + 0.01) < 1.0e-6);
            Assert.Equal(1.0 / ShieldSolver.BondDamageTime, ShieldSolver.BondRate(limit, limit), 12);
            Assert.Equal(ShieldSolver.BondRateCap / ShieldSolver.BondDamageTime, ShieldSolver.BondRate(limit, 5000.0), 12);
            double previous = 0.0;
            for (double t = 400.0; t <= 900.0; t += 0.5)
            {
                double rate = ShieldSolver.BondRate(limit, t);
                Assert.True(rate >= previous);
                Assert.True(rate - previous <= 0.03 * rate + 1.0e-5, "no cliff at " + t);
                previous = rate;
            }
            Assert.Equal(0.0, ShieldSolver.BondRate(0.0, 800.0));
            Assert.Equal(0.0, ShieldSolver.BondRate(limit, double.NaN));
            Assert.Equal(0.0, ShieldSolver.BondRate(double.PositiveInfinity, 800.0));

            // Two profiles that differ only in the native fallback take the same bond damage.
            MaterialDefinition a = TestMaterials.Ablator();
            MaterialDefinition b = TestMaterials.Ablator();
            b.MaxSurfaceTemp = 9000.0;
            ShieldState sa = Stocked(a, null, 0.05);
            ShieldState sb = Stocked(b, null, 0.05);
            var ra = new ShieldStepResult(sa.Count);
            var rb = new ShieldStepResult(sb.Count);
            for (int i = 0; i < 4000; i++)
            {
                ShieldSolver.Step(a, sa, Input(590.0, 0.05), ra);   // below onset: only the bond is at stake
                ShieldSolver.Step(b, sb, Input(590.0, 0.05), rb);
            }
            Assert.True(sa.Integrity[0] < 1.0, "the bond of a thin layer under a hot skin should degrade");
            for (int i = 0; i < sa.Count; i++)
            {
                Assert.Equal(sa.Integrity[i], sb.Integrity[i]);
            }
        }

        [Fact]
        public void SurfaceDeteriorationIsAllocatedByWeightAndKeepsItsMean()
        {
            MaterialDefinition tile = TestMaterials.Tile();         // damage 1600 K, fallback 1750 K
            Assert.Equal(0.0, ShieldSolver.SurfaceRate(tile, 1600.0, 1.0, 1.0));
            Assert.Equal(0.25 / ShieldSolver.SurfaceDamageTime, ShieldSolver.SurfaceRate(tile, 1675.0, 1.0, 1.0), 12);
            Assert.Equal(1.0 / ShieldSolver.SurfaceDamageTime, ShieldSolver.SurfaceRate(tile, 1750.0, 1.0, 1.0), 12);
            Assert.Equal(1.4 / ShieldSolver.SurfaceDamageTime, ShieldSolver.SurfaceRate(tile, 1750.0, 1.4, 1.0), 12);
            Assert.Equal(0.0, ShieldSolver.SurfaceRate(TestMaterials.Ablator(), 2400.0, 1.0, 1.0));      // no such mechanism

            ShieldState state = Stocked(tile);
            var result = new ShieldStepResult(state.Count);
            ShieldStepInput input = Input(1700.0, 0.02, 5.0e6, 0.5);
            ShieldSolver.Step(tile, state, input, result);
            double mean = 0.0, min = 1.0, max = 0.0;
            for (int i = 0; i < state.Count; i++)
            {
                double loss = 1.0 - state.Integrity[i];
                mean += loss * state.Layout.AreaFraction(i);
                min = Math.Min(min, loss);
                max = Math.Max(max, loss);
            }
            // Before any cell fails the area-mean rate is the unweighted rate: the weight only allocates.
            Assert.Equal(ShieldSolver.SurfaceRate(tile, 1700.0, 1.0, 1.0) * 0.02, mean, 12);
            Assert.True(max > min * 1.5);
        }

        [Fact]
        public void ATileOverItsLimitGoesInSecondsNotInOneStep()
        {
            MaterialDefinition tile = TestMaterials.Tile();
            ShieldState state = Stocked(tile);
            var result = new ShieldStepResult(state.Count);
            double time = 0.0;
            ShieldSolver.Step(tile, state, Input(1745.0, 0.02, 5.0e6), result);
            Assert.True(state.MeanIntegrity > 0.99);                // one step does almost nothing
            while (state.ExposedArea(false) < 1.0 - 1.0e-9 && time < 120.0)
            {
                ShieldSolver.Step(tile, state, Input(1745.0, 0.02, 5.0e6), result);
                time += 0.02;
            }
            Assert.InRange(time, 4.0, 60.0);                        // every tile has given way within seconds
            Assert.Equal(state.Construction.CarrierLimitTemp, result.NativeLimit);   // nothing protects: the bare carrier's limit
            // The structure behind is not destroyed by exposure alone: it gives way as it heats.
            Assert.False(result.FailureDue);
            double more = 0.0;
            while (!result.FailureDue && more < 300.0)
            {
                ShieldSolver.Step(tile, state, Input(1745.0, 0.02, 5.0e6), result);
                more += 0.02;
            }
            Assert.True(result.FailureDue);
            Assert.InRange(more, 10.0, 200.0);
        }

        [Fact]
        public void ACellThatLosesItsIntegrityShedsWhatItHeldWithoutCooling()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = Stocked(m, null, 0.02);             // thin enough for the bond to fail first
            var result = new ShieldStepResult(state.Count);
            double shed = 0.0, sink = 0.0, pyrolysed = 0.0;
            double start = state.TotalInventory;
            for (int i = 0; i < 60000 && state.ExposedArea(true) < 1.0; i++)
            {
                ShieldSolver.Step(m, state, Input(595.0, 0.05), result);        // below the ablation onset
                shed += result.ShedMass;
                sink += result.HeatAbsorbed;
                pyrolysed += result.PyrolysedMass;
            }
            Assert.Equal(1.0, state.ExposedArea(true), 9);
            Assert.Equal(0.0, sink);
            Assert.Equal(0.0, pyrolysed);
            Assert.Equal(start, shed, 9);                           // failure before zero: the layer was still there
            Assert.Equal(0.0, state.TotalInventory);
            // A cold, bare carrier is not a structural failure; but nothing protects, so the native limit drops.
            Assert.False(result.FailureDue);
            Assert.Equal(state.Construction.CarrierLimitTemp, result.NativeLimit);
        }

        [Fact]
        public void TheSinkStaysInsideItsBudgetWhenSomeCellsCannotMeetTheirDemand()
        {
            // Counterexample: a cell asked for more than it holds must not let the budget scale be computed
            // on clamped amounts and applied to unclamped ones.
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = Stocked(m);
            for (int i = 0; i < state.Count; i += 2)
            {
                state.Inventory[i] = 1.0e-4;                        // half the cells nearly empty
            }
            var result = new ShieldStepResult(state.Count);
            foreach (double capacity in new[] { 1.0, 5.0, 30.0, 200.0 })
            {
                ShieldState s = Stocked(m);
                for (int i = 0; i < s.Count; i += 2)
                {
                    s.Inventory[i] = 1.0e-4;
                }
                ShieldStepInput input = Input(2200.0, 0.5, 5.0e6, 0.3, capacity: capacity);
                input.DynamicPressure = 120000.0;
                ShieldSolver.Step(m, s, input, result);
                double budget = capacity * (2200.0 - m.Ablation.OnsetTemp);
                Assert.True(result.HeatAbsorbed <= budget * (1.0 + 1.0e-9), "sink " + result.HeatAbsorbed + " over budget " + budget);
                Assert.Equal(result.PyrolysedMass * m.Ablation.HeatOfAblation, result.HeatAbsorbed, 6);
                for (int i = 0; i < s.Count; i++)
                {
                    Assert.True(s.Inventory[i] >= 0.0);
                }
            }
        }

        [Fact]
        public void ACellThatGivesWayMidStepConsumesOnlyUntilThen()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = Stocked(m);
            ShieldState reference = Stocked(m);
            state.ThermalInitialised = reference.ThermalInitialised = true;
            for (int i = 0; i < state.Count; i++)
            {
                state.SubstrateTemp[i] = reference.SubstrateTemp[i] = 300.0;
                state.LayerTemp[i] = reference.LayerTemp[i] = 300.0;
            }
            // Cell 6 has a hot bond and almost no integrity left: it gives way early in the step.
            state.SubstrateTemp[6] = 700.0;
            state.Integrity[6] = 0.05;
            var result = new ShieldStepResult(state.Count);
            var full = new ShieldStepResult(state.Count);
            ShieldStepInput input = Input(1600.0, 0.5, 5.0e6);
            ShieldSolver.Step(m, reference, input, full);
            ShieldSolver.Step(m, state, input, result);
            double rate = ShieldSolver.BondRate(m.BondLimitTemp, 700.0);
            double window = 0.05 / (rate * 0.5);
            Assert.InRange(window, 0.01, 0.9);
            double fullCell = reference.Pyrolysed[6];
            Assert.Equal(fullCell * window, state.Pyrolysed[6], 9);  // pyrolysis only until it gave way
            Assert.Equal(0.0, state.Integrity[6]);
            Assert.Equal(0.0, state.Inventory[6]);
            Assert.Equal(reference.Capacity[6] - fullCell * window, result.ShedMass, 9);   // the rest is shed without cooling
            Assert.Equal((1.0 - window) * 0.5, result.ExposedTime[6], 9);
        }

        [Fact]
        public void ACellThatEmptiesSharesWhatItHadBetweenTheChannelsByRate()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = Stocked(m);
            state.Inventory[2] = 1.0e-5;
            var result = new ShieldStepResult(state.Count);
            ShieldStepInput input = Input(1800.0, 0.5, 5.0e6);
            input.DynamicPressure = 150000.0;                        // shear and pyrolysis at once
            input.Settings.Damage = false;
            ShieldSolver.Step(m, state, input, result);
            Assert.Equal(0.0, state.Inventory[2]);
            Assert.True(state.Pyrolysed[2] > 0.0 && state.Pyrolysed[2] < 1.0e-5);   // not all of it went to pyrolysis
            Assert.True(result.ExposedTime[2] > 0.0);
        }

        [Fact]
        public void DifficultyCanSwitchDamageOffAndScaleIt()
        {
            MaterialDefinition tile = TestMaterials.Tile();
            ShieldState off = Stocked(tile);
            ShieldState one = Stocked(tile);
            ShieldState two = Stocked(tile);
            var result = new ShieldStepResult(off.Count);
            ShieldStepInput input = Input(1700.0);
            input.Settings = new ShieldSettings { Damage = false, CellVariation = 0.0 };
            ShieldSolver.Step(tile, off, input, result);
            input.Settings = new ShieldSettings { CellVariation = 0.0 };
            ShieldSolver.Step(tile, one, input, result);
            input.Settings = new ShieldSettings { Severity = 2.0, CellVariation = 0.0 };
            ShieldSolver.Step(tile, two, input, result);
            Assert.Equal(1.0, off.MeanIntegrity, 12);
            Assert.Equal(2.0 * (1.0 - one.MeanIntegrity), 1.0 - two.MeanIntegrity, 12);
        }

        [Fact]
        public void CellBiasIsFixedSmallAndOptional()
        {
            Assert.Equal(1.0, ShieldSolver.CellBias(0u, 3, 0.02));
            Assert.Equal(1.0, ShieldSolver.CellBias(77u, 3, 0.0));
            bool differs = false;
            for (int cell = 0; cell < 25; cell++)
            {
                double bias = ShieldSolver.CellBias(123456u, cell, 0.02);
                Assert.InRange(bias, 0.98, 1.02);
                Assert.Equal(bias, ShieldSolver.CellBias(123456u, cell, 0.02));     // never rerolled
                differs |= Math.Abs(bias - ShieldSolver.CellBias(123456u, (cell + 1) % 25, 0.02)) > 1.0e-6;
                Assert.InRange(ShieldSolver.CellBias(123456u, cell, 5.0), 0.90, 1.10);
            }
            Assert.True(differs);
            Assert.NotEqual(ShieldSolver.CellBias(1u, 4, 0.02), ShieldSolver.CellBias(2u, 4, 0.02));
        }

        // ----- exposure, leak, failure -----

        [Fact]
        public void AnExposedCellPassesOnAShareOfWhatFallsOnIt()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = Stocked(m);
            state.Inventory[10] = 0.0;
            var result = new ShieldStepResult(state.Count);
            ShieldStepInput input = Input(1500.0, 0.02, 5.0e6, 0.4);
            ShieldSolver.Step(m, state, input, result);
            double expected = ShieldSolver.BreachShare * 5.0e6 * state.Layout.AreaFraction(10) * result.Weights[10] * 0.02;
            Assert.Equal(expected, result.BreachEnergy[10], 6);
            Assert.Equal(expected, result.BreachTotal, 6);
            for (int i = 0; i < state.Count; i++)
            {
                if (i != 10)
                {
                    Assert.Equal(0.0, result.BreachEnergy[i]);
                }
            }

            // Sink and leak share one budget; the sink is served first.
            input.ExposedSkinHeatCapacity = 7.0;
            ShieldState tight = Stocked(m);
            tight.Inventory[10] = 0.0;
            ShieldSolver.Step(m, tight, input, result);
            double budget = 7.0 * (1500.0 - m.Ablation.OnsetTemp);
            Assert.True(result.HeatAbsorbed > 0.0 && result.BreachTotal > 0.0);
            Assert.True(result.BreachTotal < expected, "the leak must be cut back by the shared budget");
            Assert.Equal(budget, result.HeatAbsorbed + result.BreachTotal, 6);

            // A budget smaller than the sink alone leaves nothing for the leak.
            input.ExposedSkinHeatCapacity = 2.0;
            tight = Stocked(m);
            tight.Inventory[10] = 0.0;
            ShieldSolver.Step(m, tight, input, result);
            Assert.Equal(2.0 * (1500.0 - m.Ablation.OnsetTemp), result.HeatAbsorbed, 6);
            Assert.Equal(0.0, result.BreachTotal);

            // Switched off by difficulty, or not heating: nothing moves.
            input = Input(1500.0, 0.02, 5.0e6, 0.4);
            input.Settings = new ShieldSettings { Transfer = false, CellVariation = 0.0 };
            ShieldSolver.Step(m, state, input, result);
            Assert.Equal(0.0, result.BreachTotal);
            ShieldSolver.Step(m, state, Input(1500.0, 0.02, 0.0, 0.4), result);
            Assert.Equal(0.0, result.BreachTotal);
        }

        [Fact]
        public void TheFaceShowsTheAreaWeightedEmissivityOfItsCells()
        {
            MaterialDefinition m = TestMaterials.Ablator();         // 0.7 virgin, 0.9 charred
            ShieldState state = Stocked(m);
            state.Construction.BareEmissivity = 0.35;
            Assert.Equal(0.7, ShieldSolver.Emissivity(m, state), 12);
            state.Inventory[7] = 0.0;                               // bare
            state.Pyrolysed[20] = state.Capacity[20];               // fully charred
            double a7 = state.Layout.AreaFraction(7), a20 = state.Layout.AreaFraction(20);
            Assert.Equal(0.7 * (1.0 - a7 - a20) + 0.35 * a7 + 0.9 * a20, ShieldSolver.Emissivity(m, state), 12);
        }

        [Fact]
        public void HotExposedStructureGivesWayGraduallyAndOnlyOnce()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = Stocked(m);
            for (int i = 0; i < 8; i++)
            {
                state.Inventory[16 + i] = 0.0;                      // the outer band is gone: about 55 % of the face
            }
            var result = new ShieldStepResult(state.Count);
            double time = 0.0, firstLoss = -1.0;
            while (!result.FailureDue && time < 600.0)
            {
                ShieldStepInput input = Input(1900.0, 0.05, 6.0e6);
                ShieldSolver.Step(m, state, input, result);
                time += 0.05;
                if (firstLoss < 0.0 && state.Structure < 1.0)
                {
                    firstLoss = time;
                }
            }
            Assert.True(result.FailureDue);
            Assert.True(firstLoss > 5.0, "the exposed substrate lags the skin before the structure starts to give, started at " + firstLoss);
            Assert.True(time > firstLoss + 2.0, "and then gives way over seconds, not in a step");
            Assert.True(state.ExposedArea(true) < 1.0, "cells were still protected when the shield failed");
            Assert.Equal(m.MaxSurfaceTemp, result.NativeLimit);     // nothing is applied until the adapter decides

            ShieldSolver.ApplyFailure(state, ShieldFailure.ProtectionLost);
            ShieldSolver.Step(m, state, Input(900.0, 0.05, 1.0e6), result);
            Assert.False(result.FailureDue);                        // once
            Assert.Equal(state.Construction.CarrierLimitTemp, result.NativeLimit);
            Assert.Equal(0.0, result.HeatAbsorbed);                 // the sink has stopped
            Assert.Equal(state.Construction.BareEmissivity, result.Emissivity, 12);
            Assert.True(result.BreachTotal > 0.0);                  // and the whole face leaks
        }

        [Fact]
        public void WithFailureSwitchedOffTheShieldNeverFails()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = Stocked(m);
            for (int i = 0; i < 24; i++)
            {
                state.Inventory[i] = 0.0;
            }
            var result = new ShieldStepResult(state.Count);
            ShieldStepInput input = Input(1900.0, 0.05, 6.0e6);
            input.Settings = new ShieldSettings { Failure = false, CellVariation = 0.0 };
            for (int i = 0; i < 4000; i++)
            {
                ShieldSolver.Step(m, state, input, result);
            }
            Assert.Equal(1.0, state.Structure);
            Assert.False(result.FailureDue);
            Assert.Equal(m.MaxSurfaceTemp, result.NativeLimit);     // one cell still protects
            state.Inventory[24] = 0.0;                              // the last cell
            for (int i = 0; i < 4000; i++)
            {
                ShieldSolver.Step(m, state, input, result);
            }
            Assert.False(result.FailureDue);                         // not even a total loss enacts a failure
            Assert.False(state.Failed);
            Assert.Equal(state.Construction.CarrierLimitTemp, result.NativeLimit);   // but the bare carrier's limit applies
        }

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

    public class ShieldFailurePolicyTests
    {
        [Fact]
        public void AFailedShieldOnlyLosesItsProtection()
        {
            // A failed shield never separates, so there is one failure mode.
            Assert.Equal(new[] { ShieldFailure.None, ShieldFailure.ProtectionLost },
                (ShieldFailure[])System.Enum.GetValues(typeof(ShieldFailure)));
        }

        [Fact]
        public void IntegratedHostsHeatTheirOwnInterior()
        {
            Assert.False(ShieldFailurePolicy.UsesOwnInterior("dedicated", false, false));
            Assert.True(ShieldFailurePolicy.UsesOwnInterior("multifunction", false, false));
            Assert.True(ShieldFailurePolicy.UsesOwnInterior("dedicated", true, false));
            Assert.True(ShieldFailurePolicy.UsesOwnInterior("dedicated", false, true));
        }
    }
}
