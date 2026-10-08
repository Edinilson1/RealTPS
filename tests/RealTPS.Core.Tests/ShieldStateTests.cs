using System.Collections.Generic;
using RealTPS.Core.Cfg;
using RealTPS.Core.Materials;
using RealTPS.Core.Shield;
using Xunit;

namespace RealTPS.Core.Tests
{
    public class ShieldStateTests
    {
        internal static ShieldConstruction Construction(double area = 1.27, double layer = 0.05)
        {
            return new ShieldConstruction { Area = area, LayerThickness = layer };
        }

        private static ShieldState Sample()
        {
            ShieldState state = ShieldState.Uniform(new ShieldLayout(8, 2, 2.0), Construction(), 30.0, 40.0, 10.0);
            state.Construction.StructureLimitTemp = 450.0;
            state.Construction.BackConductance = 35.0;
            state.Inventory[3] = 0.0;
            state.Integrity[5] = 0.25;
            state.Integrity[9] = 0.0;
            state.LayerTemp[2] = 900.0;
            state.SubstrateTemp[2] = 450.0;
            state.Received[7] = 1.5e6;
            state.Structure = 0.6;
            state.Seed = 123456u;
            state.ThermalInitialised = true;
            return state;
        }

        [Fact]
        public void UniformStateSpreadsTotalsByArea()
        {
            ShieldState state = ShieldState.Uniform(new ShieldLayout(8, 2, 2.0), Construction(), 30.0, 40.0, 10.0);
            Assert.Equal(30.0, state.TotalInventory, 9);
            Assert.Equal(40.0, state.TotalCapacity, 9);
            Assert.Equal(10.0, state.TotalPyrolysed, 9);
            Assert.Equal(1.0, state.MeanIntegrity, 12);
            for (int i = 0; i < state.Count; i++)
            {
                Assert.Equal(0.75, state.Fraction(i), 12);
                Assert.False(state.IsExposed(i, true));
            }
            // Inventory can never start above capacity, and bad numbers become zero.
            Assert.Equal(40.0, ShieldState.Uniform(ShieldLayout.Single(), Construction(), 99.0, 40.0, 0.0).TotalInventory, 9);
            Assert.Equal(0.0, ShieldState.Uniform(ShieldLayout.Single(), Construction(), double.NaN, double.PositiveInfinity, -1.0).TotalCapacity);
        }

        [Fact]
        public void ExposureDependsOnIntegrityAndForConsumablesOnInventory()
        {
            ShieldState state = Sample();
            Assert.True(state.IsExposed(3, true));      // empty cell, consumable material
            Assert.False(state.IsExposed(3, false));    // a material without inventory is not exposed by an empty cell
            Assert.True(state.IsExposed(9, true));      // integrity gone
            Assert.True(state.IsExposed(9, false));
            Assert.False(state.IsExposed(5, true));     // damaged, not gone
            double expected = state.Layout.AreaFraction(3) + state.Layout.AreaFraction(9);
            Assert.Equal(expected, state.ExposedArea(true), 12);
            Assert.Equal(state.Layout.AreaFraction(9), state.ExposedArea(false), 12);
        }

        [Fact]
        public void AFailedShieldIsExposedEverywhere()
        {
            ShieldState state = Sample();
            double shed = ShieldSolver.ApplyFailure(state, ShieldFailure.ProtectionLost);
            Assert.Equal(30.0 - 30.0 * state.Layout.AreaFraction(3), shed, 9);
            Assert.Equal(0.0, state.TotalInventory);
            Assert.Equal(0.0, state.Structure);
            Assert.Equal(1.0, state.ExposedArea(false), 12);
            for (int i = 0; i < state.Count; i++)
            {
                Assert.True(state.IsExposed(i, false));
            }
            // Once only: a second failure changes nothing.
            Assert.Equal(0.0, ShieldSolver.ApplyFailure(state, ShieldFailure.ProtectionLost));
            Assert.Equal(ShieldFailure.ProtectionLost, state.Failure);
        }

        [Fact]
        public void ThicknessComesFromTheInventoryOrFromTheConstruction()
        {
            MaterialDefinition ablator = TestMaterials.Ablator();   // 500 kg/m3
            ShieldState state = ShieldState.Uniform(new ShieldLayout(8, 2, 2.0), Construction(2.0), 500.0 * 0.04 * 2.0, 500.0 * 0.05 * 2.0, 0.0);
            for (int i = 0; i < state.Count; i++)
            {
                Assert.Equal(0.04, state.Thickness(i, ablator), 12);
            }
            state.Inventory[4] *= 0.5;
            Assert.Equal(0.02, state.Thickness(4, ablator), 12);

            ShieldState tile = ShieldState.Uniform(new ShieldLayout(8, 2, 2.0), Construction(2.0, 0.06), 0.0, 0.0, 0.0);
            Assert.Equal(0.06, tile.Thickness(0, TestMaterials.Tile()), 12);
        }

        [Fact]
        public void RoundTripsThroughItsNodeWithItsGridAndConstruction()
        {
            ShieldState original = Sample();
            string text = CfgParser.Write(original.Write());
            ShieldState copy;
            Assert.Equal(ShieldState.ReadStatus.Ok, ShieldState.Read(CfgParser.Parse(text).Nodes[0], new List<ValidationIssue>(), out copy));
            Assert.Equal(text, CfgParser.Write(copy.Write()));
            // The grid and the construction are part of the state: whatever the host says now, these stay.
            Assert.Equal(original.Layout, copy.Layout);
            Assert.Equal(1.27, copy.Construction.Area);
            Assert.Equal(450.0, copy.Construction.StructureLimitTemp);
            Assert.Equal(35.0, copy.Construction.BackConductance);
            Assert.Equal(0.6, copy.Structure);
            Assert.Equal(123456u, copy.Seed);
            Assert.True(copy.ThermalInitialised);
            // An exhausted cell stays exhausted: there is no remapping that could refill it.
            Assert.True(copy.IsExposed(3, true));
        }

        [Theory]
        [InlineData("version = 1", "version = 2", ShieldState.ReadStatus.UnsupportedVersion)]
        [InlineData("version = 1", "version = 0", ShieldState.ReadStatus.Invalid)]
        [InlineData("version = 1", "version = x", ShieldState.ReadStatus.Invalid)]
        [InlineData("layout = 8,2,2", "layout = 3,2,2", ShieldState.ReadStatus.Invalid)]
        [InlineData("layout = 8,2,2", "layout = 6,2,2", ShieldState.ReadStatus.Invalid)]
        [InlineData("structure = 0.6", "structure = 1.5", ShieldState.ReadStatus.Invalid)]
        [InlineData("structure = 0.6", "structure = NaN", ShieldState.ReadStatus.Invalid)]
        [InlineData("failure = None", "failure = Melted", ShieldState.ReadStatus.Invalid)]
        [InlineData("failure = None", "failure = 7", ShieldState.ReadStatus.Invalid)]
        [InlineData("seed = 123456", "seed = -1", ShieldState.ReadStatus.Invalid)]
        [InlineData("thermalInitialised = true", "thermalInitialised = maybe", ShieldState.ReadStatus.Invalid)]
        [InlineData("area = 1.27", "area = 0", ShieldState.ReadStatus.Invalid)]
        [InlineData("area = 1.27", "aera = 1.27", ShieldState.ReadStatus.Invalid)]
        [InlineData("backConductance = 35", "backConductance = -1", ShieldState.ReadStatus.Invalid)]
        [InlineData("substrateHeatCapacity = 7300", "substrateHeatCapacity = Infinity", ShieldState.ReadStatus.Invalid)]
        [InlineData("bareEmissivity = 0.4", "bareEmissivity = 1.5", ShieldState.ReadStatus.Invalid)]
        public void UnsafeOrNewerStatesAreRefused(string find, string replace, ShieldState.ReadStatus expected)
        {
            string text = CfgParser.Write(Sample().Write());
            Assert.Contains(find, text);
            ShieldState copy;
            var issues = new List<ValidationIssue>();
            Assert.Equal(expected, ShieldState.Read(CfgParser.Parse(text.Replace(find, replace)).Nodes[0], issues, out copy));
            Assert.Null(copy);
            Assert.NotEmpty(issues);
        }

        [Fact]
        public void InvalidCellValuesAreRefused()
        {
            foreach (string key in new[] { "inventory", "capacity", "pyrolysed", "integrity", "layerTemp", "substrateTemp", "received" })
            {
                CfgNode node = CfgParser.Parse(CfgParser.Write(Sample().Write())).Nodes[0];
                bool found = false;
                for (int i = 0; i < node.Values.Count; i++)
                {
                    if (node.Values[i].Key == key)
                    {
                        found = true;
                        node.Values[i] = new KeyValuePair<string, string>(key, node.Values[i].Value.Replace(",", ",-1,").Substring(0, node.Values[i].Value.Length));
                    }
                }
                Assert.True(found, key);
                ShieldState copy;
                Assert.Equal(ShieldState.ReadStatus.Invalid, ShieldState.Read(node, new List<ValidationIssue>(), out copy));
            }
            // A cell that holds more than its capacity is not a state this build wrote.
            ShieldState over = Sample();
            over.Inventory[0] = over.Capacity[0] * 2.0;
            ShieldState read;
            Assert.Equal(ShieldState.ReadStatus.Invalid, ShieldState.Read(over.Write(), new List<ValidationIssue>(), out read));
        }

        [Fact]
        public void TheBondLimitIsTheLowerOfMaterialAndStructure()
        {
            MaterialDefinition tile = TestMaterials.Tile();     // bond 530 K
            var c = Construction();
            Assert.Equal(530.0, c.BondLimit(tile));
            c.StructureLimitTemp = 450.0;
            Assert.Equal(450.0, c.BondLimit(tile));
            c.StructureLimitTemp = 900.0;
            Assert.Equal(530.0, c.BondLimit(tile));
            MaterialDefinition legacy = LegacyProfile.Create(1.27, 200.0, 0.4, 3300.0);
            Assert.Equal(900.0, c.BondLimit(legacy));
            c.StructureLimitTemp = 0.0;
            Assert.Equal(0.0, c.BondLimit(legacy));
        }
    }
}
