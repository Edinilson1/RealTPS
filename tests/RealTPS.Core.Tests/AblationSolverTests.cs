using System;
using RealTPS.Core.Materials;
using RealTPS.Core.Physics;
using Xunit;

namespace RealTPS.Core.Tests
{
    public class AblationSolverTests
    {
        private static StepInput Nominal()
        {
            return new StepInput
            {
                Dt = 0.02,
                SkinTemp = 1800.0,
                SkinHeatCapacity = 2.0e5,
                Area = 1.2,
                RemainingMass = 30.0,
                DynamicPressure = 10000.0,
                OxygenAtmosphere = true
            };
        }

        [Fact]
        public void PyrolysisFollowsArealArrheniusLaw()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            StepInput input = Nominal();
            StepResult r = AblationSolver.Step(m, input);

            double expected = 5.0 * Math.Exp(-8000.0 / 1800.0) * 1.2 * 0.02;
            Assert.Equal(expected, r.PyrolysedMass, 12);
            Assert.Equal(expected * 5.0e6, r.HeatAbsorbed, 6);
            Assert.Equal(0.0, r.ShearErodedMass);
            Assert.False(r.ShearOverload);
        }

        [Fact]
        public void EnergyAndMassAreConservedEveryStep()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            var random = new Random(1234);
            for (int i = 0; i < 5000; i++)
            {
                var input = new StepInput
                {
                    Dt = random.NextDouble() * 2.0,
                    SkinTemp = 200.0 + random.NextDouble() * 4000.0,
                    SkinHeatCapacity = random.NextDouble() * 1.0e6,
                    Area = random.NextDouble() * 50.0,
                    RemainingMass = random.NextDouble() * 100.0,
                    DynamicPressure = random.NextDouble() * 400000.0,
                    OxygenAtmosphere = random.Next(2) == 0
                };
                StepResult r = AblationSolver.Step(m, input);

                Assert.True(r.PyrolysedMass >= 0.0 && r.ShearErodedMass >= 0.0 && r.OxidisedMass >= 0.0);
                Assert.True(r.ConsumedMass <= input.RemainingMass + 1.0e-12, "consumed more than available");
                Assert.Equal(r.PyrolysedMass * m.Ablation.HeatOfAblation, r.HeatAbsorbed, 6);

                // No free cooling: the sink can never take the skin below the onset temperature.
                double budget = Math.Max(0.0, input.SkinHeatCapacity * (input.SkinTemp - m.Ablation.OnsetTemp));
                Assert.True(r.HeatAbsorbed <= budget * (1.0 + 1.0e-12) + 1.0e-9, "sink exceeded the skin energy budget");
            }
        }

        [Fact]
        public void NoSinkBelowOnsetTemperature()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            StepInput input = Nominal();
            input.SkinTemp = 600.0;
            StepResult r = AblationSolver.Step(m, input);
            Assert.Equal(0.0, r.PyrolysedMass);
            Assert.Equal(0.0, r.HeatAbsorbed);
        }

        [Fact]
        public void SinkIsClampedByAvailableMass()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            StepInput input = Nominal();
            input.RemainingMass = 1.0e-5;
            input.SkinTemp = 3000.0;
            input.Dt = 1.0;
            input.SkinHeatCapacity = 1.0e9;
            StepResult r = AblationSolver.Step(m, input);
            Assert.Equal(1.0e-5, r.ConsumedMass, 15);
        }

        [Fact]
        public void SinkIsClampedBySkinEnergyBudget()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            StepInput input = Nominal();
            input.SkinTemp = 3000.0;
            input.Dt = 1.0;
            input.SkinHeatCapacity = 10.0;
            StepResult r = AblationSolver.Step(m, input);
            Assert.Equal(10.0 * (3000.0 - 600.0), r.HeatAbsorbed, 6);
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void InvalidInputsProduceNoEffect(double bad)
        {
            MaterialDefinition m = TestMaterials.Ablator();
            Func<StepInput, StepInput>[] mutations =
            {
                i => { i.Dt = bad; return i; },
                i => { i.SkinTemp = bad; return i; },
                i => { i.Area = bad; return i; },
                i => { i.RemainingMass = bad; return i; }
            };
            foreach (Func<StepInput, StepInput> mutate in mutations)
            {
                StepResult r = AblationSolver.Step(m, mutate(Nominal()));
                Assert.Equal(0.0, r.ConsumedMass);
                Assert.Equal(0.0, r.HeatAbsorbed);
            }
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(0.0)]
        [InlineData(-5.0)]
        public void UnknownHeatCapacityMeansNoSink(double capacity)
        {
            MaterialDefinition m = TestMaterials.Ablator();
            StepInput input = Nominal();
            input.SkinHeatCapacity = capacity;
            StepResult r = AblationSolver.Step(m, input);
            Assert.Equal(0.0, r.PyrolysedMass);
            Assert.Equal(0.0, r.HeatAbsorbed);
        }

        [Fact]
        public void HugeStepIsBounded()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            StepInput a = Nominal();
            a.Dt = AblationSolver.MaxStep;
            a.SkinHeatCapacity = 1.0e12;
            a.RemainingMass = 1.0e6;
            StepInput b = a;
            b.Dt = 1.0e9;
            Assert.Equal(AblationSolver.Step(m, a).PyrolysedMass, AblationSolver.Step(m, b).PyrolysedMass, 12);
        }

        [Fact]
        public void ShearErosionRemovesMassWithoutCooling()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            StepInput input = Nominal();
            input.SkinTemp = 500.0;
            input.DynamicPressure = 100000.0;
            StepResult r = AblationSolver.Step(m, input);
            Assert.True(r.ShearOverload);
            Assert.Equal(0.02 * 1.0 * 1.2 * 0.02, r.ShearErodedMass, 12);
            Assert.Equal(0.0, r.HeatAbsorbed);
            Assert.Equal(0.0, r.PyrolysedMass);
        }

        [Fact]
        public void ShearOverloadIsCapped()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            StepInput input = Nominal();
            input.SkinTemp = 500.0;
            input.DynamicPressure = 1.0e12;
            input.RemainingMass = 1.0e6;
            StepResult r = AblationSolver.Step(m, input);
            Assert.Equal(0.02 * AblationSolver.MaxShearOverload * 1.2 * 0.02, r.ShearErodedMass, 12);
        }

        [Fact]
        public void NonConsumableMaterialNeverConsumesOrCools()
        {
            MaterialDefinition m = TestMaterials.Tile();
            StepInput input = Nominal();
            input.SkinTemp = 5000.0;
            input.DynamicPressure = 1.0e9;
            StepResult r = AblationSolver.Step(m, input);
            Assert.Equal(0.0, r.ConsumedMass);
            Assert.Equal(0.0, r.HeatAbsorbed);
        }

        [Fact]
        public void LongEntryNeverOverdrawsInventory()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            double remaining = 30.0;
            double absorbed = 0.0;
            for (int i = 0; i < 200000 && remaining > 0.0; i++)
            {
                var input = new StepInput { Dt = 0.02, SkinTemp = 2400.0, SkinHeatCapacity = 5.0e5, Area = 1.2, RemainingMass = remaining, DynamicPressure = 120000.0 };
                StepResult r = AblationSolver.Step(m, input);
                remaining -= r.ConsumedMass;
                absorbed += r.HeatAbsorbed;
                Assert.True(remaining >= -1.0e-9);
            }
            Assert.True(remaining <= AblationSolver.DepletionThreshold);
            Assert.True(absorbed <= 30.0 * m.Ablation.HeatOfAblation * (1.0 + 1.0e-9));
        }

        [Fact]
        public void EmissivityBlendsFromVirginToChar()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            Assert.Equal(0.7, AblationSolver.SurfaceEmissivity(m, 0.0), 12);
            Assert.Equal(0.8, AblationSolver.SurfaceEmissivity(m, 0.025), 12);
            Assert.Equal(0.9, AblationSolver.SurfaceEmissivity(m, 0.05), 12);
            Assert.Equal(0.9, AblationSolver.SurfaceEmissivity(m, 1.0), 12);
            Assert.Equal(0.7, AblationSolver.SurfaceEmissivity(m, double.NaN), 12);
            Assert.Equal(0.85, AblationSolver.SurfaceEmissivity(TestMaterials.Tile(), 0.9), 12);
        }

        [Fact]
        public void EquivalentThicknessMatchesDensityAndArea()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            Assert.Equal(0.05, AblationSolver.EquivalentThickness(m, 500.0 * 0.05 * 1.2, 1.2), 12);
            Assert.Equal(0.0, AblationSolver.EquivalentThickness(m, 10.0, 0.0));
            Assert.Equal(0.0, AblationSolver.EquivalentThickness(m, double.NaN, 1.0));
        }
    }
}
