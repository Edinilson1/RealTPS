using System;
using System.Collections.Generic;
using RealTPS.Core.Cfg;
using RealTPS.Core.Hosts;
using RealTPS.Core.Materials;
using RealTPS.Core.Shield;
using Xunit;

namespace RealTPS.Core.Tests
{
    public class ThermalLedgerTests
    {
        private const double Tau = ThermalLedger<string>.RelaxationTime;

        [Fact]
        public void TheLedgerWorksInJoulesAndJoulesPerKelvin()
        {
            // The engine keeps 1 kJ/K as "1"; the adapter hands the ledger 1000 J/K. 1000 J then project 1 K.
            var ledger = new ThermalLedger<string>();
            ledger.Credit("skin", 1000.0);
            Assert.Equal(301.0, ledger.Projected("skin", 300.0, 1000.0), 12);
            ledger.Debit("skin", 3000.0);
            Assert.Equal(298.0, ledger.Projected("skin", 300.0, 1000.0), 12);
            Assert.Equal(-2000.0, ledger.Net("skin"));
            Assert.Equal(0.0, ledger.Net("other"));
            ledger.Clear();
            Assert.Equal(0.0, ledger.Net("skin"));
        }

        [Fact]
        public void TwoEqualReservoirsNeverCross()
        {
            // Counterexample: 1000 J/K each, 1000 K and 500 K, a step as long as the relaxation time.
            var ledger = new ThermalLedger<string>();
            double granted = ledger.Transfer("a", 1000.0, 1000.0, "b", 1000.0, 500.0, 1.0e12, Tau);
            Assert.Equal(500.0 * 500.0 * (1.0 - Math.Exp(-1.0)), granted, 6);
            double a = ledger.Projected("a", 1000.0, 1000.0);
            double b = ledger.Projected("b", 500.0, 1000.0);
            Assert.True(a > b);
            Assert.Equal(500.0 * Math.Exp(-1.0), a - b, 9);         // the gap closes by exactly 1 - 1/e
            Assert.Equal(1500.0, a + b, 9);                         // and energy is conserved

            // Even an absurdly long step only equalises.
            ledger.Clear();
            ledger.Transfer("a", 1000.0, 1000.0, "b", 10.0, 500.0, 1.0e12, 1.0e6);
            Assert.True(ledger.Projected("a", 1000.0, 1000.0) >= ledger.Projected("b", 500.0, 10.0) - 1.0e-9);
        }

        [Theory]
        [InlineData(1000.0, 1000.0)]
        [InlineData(5000.0, 40.0)]
        [InlineData(3.0, 800.0)]
        public void ASaturatedPairClosesTheSameShareInOneLongStepAsInManyShortOnes(double donorCapacity, double recipientCapacity)
        {
            double longGap = Gap(donorCapacity, recipientCapacity, 1, 1.0);
            double shortGap = Gap(donorCapacity, recipientCapacity, 50, 0.02);
            Assert.Equal(300.0 * Math.Exp(-1.0 / Tau), longGap, 9);
            Assert.Equal(longGap, shortGap, 9);
        }

        private static double Gap(double donorCapacity, double recipientCapacity, int steps, double dt)
        {
            double donor = 900.0, recipient = 600.0;
            var ledger = new ThermalLedger<int>();
            for (int i = 0; i < steps; i++)
            {
                ledger.Clear();
                double granted = ledger.Transfer(0, donorCapacity, donor, 1, recipientCapacity, recipient, double.MaxValue, dt);
                donor -= granted / donorCapacity;
                recipient += granted / recipientCapacity;
            }
            return donor - recipient;
        }

        [Fact]
        public void ATransferPlannedAfterASinkUsesTheGapTheSinkLeaves()
        {
            // 1000 J/K each, 1000 K and 800 K, a 150 kJ sink on the donor, f = 0.5.
            double dt = Tau * Math.Log(2.0);
            var ledger = new ThermalLedger<string>();
            ledger.Debit("donor", 150000.0);
            double granted = ledger.Transfer("donor", 1000.0, 1000.0, "recipient", 1000.0, 800.0, 1.0e9, dt);
            Assert.Equal(500.0 * 50.0 * 0.5, granted, 6);           // from the projected 50 K gap, not from 200 K
            Assert.True(ledger.Projected("donor", 1000.0, 1000.0) > ledger.Projected("recipient", 800.0, 1000.0));

            // A sink that takes the donor below the recipient leaves nothing to pass on.
            ledger.Clear();
            ledger.Debit("donor", 250000.0);
            Assert.Equal(0.0, ledger.Transfer("donor", 1000.0, 1000.0, "recipient", 1000.0, 800.0, 1.0e9, dt));
        }

        [Fact]
        public void TwoDonorsFeedingOneSmallReceiverStayInsideItsBound()
        {
            var ledger = new ThermalLedger<string>();
            const double small = 5.0;                               // J/K: a tiny part behind two big shields
            double first = ledger.Transfer("hot", 50000.0, 1400.0, "small", small, 400.0, 1.0e9, 0.02);
            double second = ledger.Transfer("warm", 50000.0, 900.0, "small", small, 400.0, 1.0e9, 0.02);
            double receiver = ledger.Projected("small", 400.0, small);
            Assert.True(first > 0.0 && second > 0.0);
            Assert.True(receiver < 900.0, "the receiver must stay below the cooler donor, got " + receiver);
            Assert.True(receiver < ledger.Projected("warm", 900.0, 50000.0));
            // The second donor saw the receiver already warmed by the first.
            var alone = new ThermalLedger<string>();
            Assert.True(second < alone.Transfer("warm", 50000.0, 900.0, "small", small, 400.0, 1.0e9, 0.02));
        }

        [Fact]
        public void AReservoirThatReceivesAndExportsInTheSameStepIsProjectedBothWays()
        {
            var ledger = new ThermalLedger<string>();
            double into = ledger.Transfer("front", 2000.0, 1500.0, "middle", 2000.0, 700.0, 1.0e9, 0.1);
            double onward = ledger.Transfer("middle", 2000.0, 700.0, "back", 2000.0, 300.0, 1.0e9, 0.1);
            var alone = new ThermalLedger<string>();
            Assert.True(onward > alone.Transfer("middle", 2000.0, 700.0, "back", 2000.0, 300.0, 1.0e9, 0.1));
            Assert.Equal(into - onward, ledger.Net("middle"), 9);
            double front = ledger.Projected("front", 1500.0, 2000.0), middle = ledger.Projected("middle", 700.0, 2000.0), back = ledger.Projected("back", 300.0, 2000.0);
            Assert.True(front > middle && middle > back);
        }

        [Fact]
        public void SkinToOwnInteriorMovesBothTemperatures()
        {
            // Same part, two reservoirs: a part-keyed ledger would cancel the debit against the credit.
            var ledger = new ThermalLedger<KeyValuePair<string, int>>();
            var skin = new KeyValuePair<string, int>("pod", 0);
            var interior = new KeyValuePair<string, int>("pod", 1);
            double granted = ledger.Transfer(skin, 800.0, 1200.0, interior, 60000.0, 320.0, 4000.0, 0.02);
            Assert.Equal(4000.0, granted, 9);
            Assert.Equal(1200.0 - 5.0, ledger.Projected(skin, 1200.0, 800.0), 9);
            Assert.Equal(320.0 + 4000.0 / 60000.0, ledger.Projected(interior, 320.0, 60000.0), 9);
        }

        [Fact]
        public void CoalescedRequestsMakeTheGrantIndependentOfTheGrid()
        {
            // The same exposed area asking as one request or as many: the adapter sums per reservoir pair first.
            var one = new ThermalLedger<string>();
            double single = one.Transfer("shield", 3000.0, 1300.0, "tank", 900.0, 350.0, 80000.0, 0.02);
            var many = new ThermalLedger<string>();
            double sum = 0.0;
            for (int cell = 0; cell < 16; cell++)
            {
                sum += 80000.0 / 16.0;
            }
            double coalesced = many.Transfer("shield", 3000.0, 1300.0, "tank", 900.0, 350.0, sum, 0.02);
            Assert.Equal(single, coalesced, 9);
            Assert.True(single < 80000.0, "the case must be saturated for the comparison to mean anything");
        }

        [Fact]
        public void NothingMovesUphillOrFromBadNumbers()
        {
            var ledger = new ThermalLedger<string>();
            Assert.Equal(0.0, ledger.Transfer("a", 1000.0, 400.0, "b", 1000.0, 500.0, 1.0e6, 0.02));
            Assert.Equal(0.0, ledger.Transfer("a", 1000.0, 500.0, "b", 1000.0, 500.0, 1.0e6, 0.02));
            Assert.Equal(0.0, ledger.Transfer("a", 0.0, 900.0, "b", 1000.0, 500.0, 1.0e6, 0.02));
            Assert.Equal(0.0, ledger.Transfer("a", 1000.0, 900.0, "b", double.NaN, 500.0, 1.0e6, 0.02));
            Assert.Equal(0.0, ledger.Transfer("a", 1000.0, double.NaN, "b", 1000.0, 500.0, 1.0e6, 0.02));
            Assert.Equal(0.0, ledger.Transfer("a", 1000.0, 900.0, "b", 1000.0, 500.0, 0.0, 0.02));
            Assert.Equal(0.0, ledger.Transfer("a", 1000.0, 900.0, "b", 1000.0, 500.0, -5.0, 0.02));
            Assert.Equal(0.0, ledger.Transfer("a", 1000.0, 900.0, "b", 1000.0, 500.0, 1.0e6, 0.0));
            ledger.Debit("a", -4.0);
            ledger.Credit("a", double.PositiveInfinity);
            Assert.Equal(0.0, ledger.Net("a"));
            Assert.Equal(700.0, ledger.Projected("a", 700.0, 0.0));
        }
    }

    public class ShieldMapTests
    {
        private static ShieldMapData Data(ShieldState state, MaterialDefinition m)
        {
            var data = new ShieldMapData(ShieldLayout.MaxCells);
            data.Fill(m, state, new ShieldSettings { CellVariation = 0.0 });
            return data;
        }

        private static void Point(ShieldLayout layout, int cell, out double x, out double y)
        {
            double r = layout.SampleRadius(cell);
            x = r * Math.Cos(layout.Angle(cell));
            y = r * Math.Sin(layout.Angle(cell));
        }

        [Fact]
        public void TheFieldReturnsEachCellsValueAtItsSamplePointAndNeverLeavesTheRange()
        {
            var layout = new ShieldLayout(8, 2, 2.0);
            var values = new double[layout.Count];
            var flags = new CellFlags[layout.Count];
            var random = new Random(7);
            double min = double.MaxValue, max = double.MinValue;
            for (int i = 0; i < layout.Count; i++)
            {
                values[i] = random.NextDouble();
                min = Math.Min(min, values[i]);
                max = Math.Max(max, values[i]);
            }
            for (int i = 0; i < layout.Count; i++)
            {
                double x, y;
                Point(layout, i, out x, out y);
                Assert.Equal(values[i], ShieldField.Sample(layout, values, flags, x, y), 9);
            }
            for (int k = 0; k < 20000; k++)
            {
                double x = random.NextDouble() * 2.0 - 1.0, y = random.NextDouble() * 2.0 - 1.0;
                double v = ShieldField.Sample(layout, values, flags, x, y);
                if (x * x + y * y > 1.0)
                {
                    Assert.True(double.IsNaN(v));
                }
                else
                {
                    Assert.InRange(v, min - 1.0e-12, max + 1.0e-12);    // nothing more extreme than a cell holds
                }
            }
        }

        [Fact]
        public void TheFieldIsContinuousAcrossSectorsBandsAndTheWrap()
        {
            var layout = new ShieldLayout(8, 2, 2.0);
            var values = new double[layout.Count];
            var flags = new CellFlags[layout.Count];
            var random = new Random(11);
            for (int i = 0; i < layout.Count; i++)
            {
                values[i] = random.NextDouble();
            }
            const double step = 1.0e-4;
            // Around circles (crossing every sector edge and the 2 pi wrap) and along radii (crossing every band edge).
            foreach (double radius in new[] { 0.05, 0.2, 0.45, 0.7, 0.95 })
            {
                double previous = ShieldField.Sample(layout, values, flags, radius, 0.0);
                for (double angle = step; angle <= 2.0 * Math.PI + step; angle += step)
                {
                    double v = ShieldField.Sample(layout, values, flags, radius * Math.Cos(angle), radius * Math.Sin(angle));
                    Assert.True(Math.Abs(v - previous) < 2.0e-3, "jump around r=" + radius + " at angle " + angle);
                    previous = v;
                }
            }
            foreach (double angle in new[] { 0.0, 0.3, 1.9, 4.4 })
            {
                double previous = ShieldField.Sample(layout, values, flags, 0.0, 0.0);
                for (double radius = step; radius < 1.0; radius += step)
                {
                    double v = ShieldField.Sample(layout, values, flags, radius * Math.Cos(angle), radius * Math.Sin(angle));
                    Assert.True(Math.Abs(v - previous) < 5.0e-3, "jump along angle " + angle + " at r=" + radius);
                    previous = v;
                }
            }
        }

        [Fact]
        public void AUniformLayerIsAUniformField()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = ShieldSolverTests.Stocked(m, null, 0.6);
            ShieldMapData data = Data(state, m);
            var random = new Random(3);
            for (int k = 0; k < 2000; k++)
            {
                double angle = random.NextDouble() * 2.0 * Math.PI, r = Math.Sqrt(random.NextDouble());
                Assert.Equal(0.6, ShieldField.Sample(data.Layout, data.Values(ShieldView.Reserve), data.Flags, r * Math.Cos(angle), r * Math.Sin(angle)), 12);
            }
            // Kilograms differ between cells of different area; the view does not show kilograms.
            Assert.NotEqual(state.Inventory[0], state.Inventory[16]);
        }

        [Fact]
        public void FlaggedCellsAreFlatAndGiveNothingToTheirNeighbours()
        {
            var layout = new ShieldLayout(8, 2, 2.0);
            var values = new double[layout.Count];
            var flags = new CellFlags[layout.Count];
            for (int i = 0; i < layout.Count; i++)
            {
                values[i] = 1.0;
            }
            const int breached = 9;                                 // middle band, sector 1
            values[breached] = 0.0;
            flags[breached] = CellFlags.Exposed;
            var random = new Random(5);
            for (int k = 0; k < 20000; k++)
            {
                double angle = random.NextDouble() * 2.0 * Math.PI, r = Math.Sqrt(random.NextDouble());
                double x = r * Math.Cos(angle), y = r * Math.Sin(angle);
                double v = ShieldField.Sample(layout, values, flags, x, y);
                // Hard edge: 0 inside the breach, exactly 1 everywhere else. No soft patch, no half-breached neighbour.
                Assert.Equal(layout.CellAt(x, y) == breached ? 0.0 : 1.0, v, 12);
            }

            // An intact cell surrounded by flagged ones shows its own value up to its edge.
            for (int i = 0; i < layout.Count; i++)
            {
                values[i] = 0.1;
                flags[i] = CellFlags.CriticalIntegrity;
            }
            values[breached] = 0.9;
            flags[breached] = CellFlags.None;
            for (int k = 0; k < 20000; k++)
            {
                double angle = random.NextDouble() * 2.0 * Math.PI, r = Math.Sqrt(random.NextDouble());
                double x = r * Math.Cos(angle), y = r * Math.Sin(angle);
                Assert.Equal(layout.CellAt(x, y) == breached ? 0.9 : 0.1, ShieldField.Sample(layout, values, flags, x, y), 12);
            }
        }

        [Fact]
        public void FlagsComeFromTheCellsOwnValuesAndNeverClearByGettingWorse()
        {
            MaterialDefinition m = TestMaterials.Ablator();         // bond limit 520 K
            ShieldState state = ShieldSolverTests.Stocked(m);
            state.ThermalInitialised = true;
            for (int i = 0; i < state.Count; i++)
            {
                state.SubstrateTemp[i] = 300.0;
            }
            state.Inventory[1] = 0.0;                               // exposed
            state.Integrity[2] = 0.19;                              // critical
            state.Integrity[3] = 0.2;                               // not critical
            state.Inventory[4] = state.Capacity[4] * 0.09;          // low reserve
            state.Inventory[5] = state.Capacity[5] * 0.10;          // not low
            state.SubstrateTemp[6] = 420.0;                         // margin exactly 100: at risk
            state.SubstrateTemp[7] = 419.9;                         // margin 100.1: not at risk
            state.SubstrateTemp[8] = 560.0;                         // margin -40: still at risk
            state.Integrity[9] = 0.1;
            state.Inventory[9] = state.Capacity[9] * 0.01;
            state.SubstrateTemp[9] = 700.0;                         // all three at once
            ShieldMapData data = Data(state, m);

            Assert.Equal(CellFlags.Exposed, data.Flags[1]);
            Assert.Equal(CellFlags.CriticalIntegrity, data.Flags[2]);
            Assert.Equal(CellFlags.None, data.Flags[3]);
            Assert.Equal(CellFlags.LowReserve, data.Flags[4]);
            Assert.Equal(CellFlags.None, data.Flags[5]);
            Assert.Equal(CellFlags.BondAtRisk, data.Flags[6]);
            Assert.Equal(CellFlags.None, data.Flags[7]);
            Assert.Equal(CellFlags.BondAtRisk, data.Flags[8]);
            Assert.Equal(CellFlags.CriticalIntegrity | CellFlags.LowReserve | CellFlags.BondAtRisk, data.Flags[9]);
            Assert.Equal(1, data.ExposedCells);
            Assert.Equal(2, data.CriticalCells);
            Assert.Equal(2, data.LowReserveCells);
            Assert.Equal(3, data.BondRiskCells);
            Assert.Equal(state.Layout.AreaFraction(1), data.ExposedArea, 12);
            Assert.Equal(520.0 - 700.0, data.SmallestBondMargin, 9);

            // "Worst" is defined per quantity.
            Assert.Equal(1, data.WorstCell(ShieldView.Reserve));
            Assert.Equal(9, data.WorstCell(ShieldView.Integrity));
            Assert.Equal(9, data.WorstCell(ShieldView.BondMargin));
            Assert.Equal(9, data.WorstCell(ShieldView.SubstrateTemp));
            state.Received[12] = 9.0e9;
            Assert.Equal(12, Data(state, m).WorstCell(ShieldView.Received));
        }

        [Fact]
        public void TheBondMarginUsesEachCellsOwnLimit()
        {
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = ShieldSolverTests.Stocked(m);
            state.ThermalInitialised = true;
            state.Seed = 4242u;
            for (int i = 0; i < state.Count; i++)
            {
                state.SubstrateTemp[i] = 400.0;
            }
            var data = new ShieldMapData(ShieldLayout.MaxCells);
            var settings = new ShieldSettings { CellVariation = 0.05 };
            data.Fill(m, state, settings);
            bool differs = false;
            for (int i = 0; i < state.Count; i++)
            {
                double expected = 520.0 * ShieldSolver.CellBias(4242u, i, 0.05) - 400.0;
                Assert.Equal(expected, data.Values(ShieldView.BondMargin)[i], 9);
                differs |= Math.Abs(expected - 120.0) > 1.0;
            }
            Assert.True(differs);
        }

        [Fact]
        public void LegacyShieldsHaveNoDamageStateToShow()
        {
            MaterialDefinition legacy = LegacyProfile.Create(2.0, 200.0, 0.4, 3300.0);
            ShieldState state = ShieldState.Uniform(ShieldLayout.Single(), new ShieldConstruction { Area = 2.0 }, 0.0, 200.0, 200.0);
            ShieldMapData data = Data(state, legacy);
            Assert.False(data.HasDamageState);
            Assert.False(data.HasBondLimit);
            Assert.Equal(0, data.ExposedCells);
            Assert.True(double.IsNaN(data.Values(ShieldView.Integrity)[0]));
            Assert.True(double.IsNaN(data.SmallestBondMargin));
            Assert.Equal(-1, data.WorstCell(ShieldView.BondMargin));
            Assert.Equal(CellFlags.LowReserve, data.Flags[0]);
        }

        [Fact]
        public void TheImageShowsTheFaceFromOutsideWithSectorZeroAtTheTop()
        {
            const int size = 256;
            int px, py;
            // Sector zero (+x on the face) is at the top; increasing sectors (+y) are to the left.
            Assert.True(ShieldMapRenderer.TexelFromFace(0.9, 0.0, size, out px, out py));
            Assert.InRange(px, 127, 128);
            Assert.InRange(py, 0, 20);
            Assert.True(ShieldMapRenderer.TexelFromFace(0.0, 0.9, size, out px, out py));
            Assert.InRange(px, 0, 20);
            Assert.InRange(py, 127, 128);
            Assert.False(ShieldMapRenderer.TexelFromFace(1.5, 0.0, size, out px, out py));

            var layout = new ShieldLayout(8, 2, 2.0);
            var cells = new int[size * size];
            ShieldMapRenderer.CellMap(layout, size, cells);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double fx, fy, u, v;
                    ShieldMapRenderer.FaceFromTexel(x, y, size, out fx, out fy);
                    // Drawing and hit test agree at every texel centre, through the same transform.
                    Assert.Equal(layout.CellAt(fx, fy), cells[y * size + x]);
                    Assert.True(ShieldMapRenderer.TexelFromFace(fx, fy, size, out px, out py));
                    Assert.Equal(x, px);
                    Assert.Equal(y, py);
                    // The overlay contract: the UV of a face point addresses the texel of that point (v runs up).
                    ShieldMapRenderer.UvFromFace(fx, fy, out u, out v);
                    Assert.Equal(x, (int)Math.Floor(u * size));
                    Assert.Equal(y, (int)Math.Floor((1.0 - v) * size));
                }
            }
            // A mirrored face (sector direction reversed) mirrors the picture left to right.
            Assert.Equal(layout.CellAt(0.5, 0.3), cells[CellIndex(0.5, 0.3, size)]);
            Assert.NotEqual(cells[CellIndex(0.5, 0.3, size)], cells[CellIndex(0.5, -0.3, size)]);
            // Shifting by one sector turns the picture by one sector.
            double turn = 2.0 * Math.PI / 8.0;
            Assert.Equal((layout.Sector(layout.CellAt(0.6, 0.1)) + 1) % 8, layout.Sector(layout.CellAt(0.6 * Math.Cos(turn) - 0.1 * Math.Sin(turn), 0.6 * Math.Sin(turn) + 0.1 * Math.Cos(turn))));
        }

        private static int CellIndex(double x, double y, int size)
        {
            int px, py;
            ShieldMapRenderer.TexelFromFace(x, y, size, out px, out py);
            return py * size + px;
        }

        [Fact]
        public void AHealthyShieldIsDrawnWithoutDivisionsAndABreachWithAHardEdge()
        {
            const int size = 128;
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = ShieldSolverTests.Stocked(m, null, 0.8);
            ShieldMapData data = Data(state, m);
            var cells = new int[size * size];
            var rgba = new byte[size * size * 4];
            ShieldMapRenderer.CellMap(data.Layout, size, cells);
            MapScale scale = MapScale.For(ShieldView.Reserve, 520.0);

            ShieldMapRenderer.Render(data, ShieldView.Reserve, scale, cells, size, false, -1, rgba);
            int first = -1;
            for (int p = 0; p < size * size; p++)
            {
                if (cells[p] < 0)
                {
                    Assert.Equal(0, rgba[p * 4 + 3]);               // transparent outside the disc
                    continue;
                }
                if (first < 0)
                {
                    first = p;
                }
                for (int ch = 0; ch < 4; ch++)
                {
                    Assert.Equal(rgba[first * 4 + ch], rgba[p * 4 + ch]);   // one colour: no borders, no cells
                }
            }

            // The grid toggle brings the borders back.
            ShieldMapRenderer.Render(data, ShieldView.Reserve, scale, cells, size, true, -1, rgba);
            int black = 0;
            for (int p = 0; p < size * size; p++)
            {
                if (cells[p] >= 0 && rgba[p * 4] == 0 && rgba[p * 4 + 1] == 0 && rgba[p * 4 + 2] == 0)
                {
                    black++;
                }
            }
            Assert.True(black > 300);

            // One exposed cell: hatched grey inside, outlined, and its neighbours keep the healthy colour.
            state.Inventory[9] = 0.0;
            data = Data(state, m);
            ShieldMapRenderer.Render(data, ShieldView.Reserve, scale, cells, size, false, -1, rgba);
            int grey = 0, hatch = 0, healthy = 0, other = 0;
            for (int p = 0; p < size * size; p++)
            {
                if (cells[p] < 0)
                {
                    continue;
                }
                byte r = rgba[p * 4], g = rgba[p * 4 + 1], b = rgba[p * 4 + 2];
                bool isHealthy = r == rgba[first * 4] && g == rgba[first * 4 + 1] && b == rgba[first * 4 + 2];
                if (cells[p] == 9)
                {
                    if (r == 56 && g == 59 && b == 66)
                    {
                        grey++;
                    }
                    else if (r == 13 && g == 13 && b == 13)
                    {
                        hatch++;
                    }
                    Assert.False(isHealthy);
                }
                else if (isHealthy)
                {
                    healthy++;
                }
                else
                {
                    other++;                                        // only the outline of the breach
                    Assert.True(r == 0 && g == 0 && b == 0);
                }
            }
            Assert.True(grey > 50 && hatch > 10);
            Assert.True(healthy > 5000);
            Assert.InRange(other, 1, 400);

            // The selected cell is outlined in its own colour.
            ShieldMapRenderer.Render(data, ShieldView.Reserve, scale, cells, size, false, 3, rgba);
            int blue = 0;
            for (int p = 0; p < size * size; p++)
            {
                if (cells[p] == 3 && rgba[p * 4 + 2] == 255)
                {
                    blue++;
                }
            }
            Assert.True(blue > 20);
            Assert.Throws<ArgumentException>(() => ShieldMapRenderer.Render(data, ShieldView.Reserve, scale, cells, size, false, -1, new byte[10]));
        }

        [Fact]
        public void EachViewMarksOnlyWhatItMeasures()
        {
            Assert.Equal(CellFlags.Exposed | CellFlags.LowReserve, ShieldMapData.Marks(ShieldView.Reserve));
            Assert.Equal(CellFlags.CriticalIntegrity, ShieldMapData.Marks(ShieldView.Integrity));
            Assert.Equal(CellFlags.BondAtRisk, ShieldMapData.Marks(ShieldView.SubstrateTemp));
            Assert.Equal(CellFlags.BondAtRisk, ShieldMapData.Marks(ShieldView.BondMargin));
            Assert.Equal(CellFlags.None, ShieldMapData.Marks(ShieldView.Received));

            // A cell that only ran out of material: exposed in the reserve view, a sound cell in the integrity view.
            const int size = 128;
            MaterialDefinition m = TestMaterials.Ablator();
            ShieldState state = ShieldSolverTests.Stocked(m, null, 0.8);
            state.Inventory[9] = 0.0;
            ShieldMapData data = Data(state, m);
            Assert.Equal(CellFlags.Exposed, data.Flags[9]);
            var cells = new int[size * size];
            var rgba = new byte[size * size * 4];
            ShieldMapRenderer.CellMap(data.Layout, size, cells);
            ShieldMapRenderer.Render(data, ShieldView.Integrity, MapScale.For(ShieldView.Integrity, 520.0), cells, size, false, -1, rgba);
            int first = -1;
            for (int p = 0; p < size * size; p++)
            {
                if (cells[p] < 0)
                {
                    continue;
                }
                if (first < 0)
                {
                    first = p;
                }
                for (int ch = 0; ch < 3; ch++)
                {
                    Assert.Equal(rgba[first * 4 + ch], rgba[p * 4 + ch]);   // every cell sound: one colour, no hatch, no edge
                }
            }

            // A cell that gave way (integrity gone) is exposed and also marked in the integrity view.
            ShieldState shed = ShieldSolverTests.Stocked(m, null, 0.8);
            shed.Inventory[9] = 0.0;
            shed.Integrity[9] = 0.0;
            data = Data(shed, m);
            Assert.Equal(CellFlags.Exposed | CellFlags.CriticalIntegrity, data.Flags[9]);
            Assert.Equal(1, data.ExposedCells);
            Assert.Equal(0, data.CriticalCells);                        // counted once, as exposed
            ShieldMapRenderer.Render(data, ShieldView.Integrity, MapScale.For(ShieldView.Integrity, 520.0), cells, size, false, -1, rgba);
            int dots = 0;
            for (int p = 0; p < size * size; p++)
            {
                if (cells[p] == 9 && rgba[p * 4] == 255 && rgba[p * 4 + 1] == 255 && rgba[p * 4 + 2] == 255)
                {
                    dots++;
                }
            }
            Assert.True(dots > 10);
        }

        [Fact]
        public void ScalesAreFixedAndPointTheRightWay()
        {
            MapScale reserve = MapScale.For(ShieldView.Reserve, 520.0);
            Assert.Equal(1.0, reserve.Goodness(1.0));
            Assert.Equal(0.0, reserve.Goodness(0.0));
            Assert.Equal(0.0, reserve.Goodness(-3.0));
            MapScale temperature = MapScale.For(ShieldView.SubstrateTemp, 520.0);
            Assert.Equal(1.0, temperature.Goodness(250.0));
            Assert.Equal(0.0, temperature.Goodness(520.0));
            Assert.Equal(0.0, temperature.Goodness(900.0));
            MapScale margin = MapScale.For(ShieldView.BondMargin, 520.0);
            Assert.Equal(0.0, margin.Goodness(-50.0));
            Assert.Equal(1.0, margin.Goodness(400.0));
            Assert.Equal(0.0, MapScale.For(ShieldView.Received, 0.0).Goodness(double.NaN));
            Assert.True(ShieldMapData.LowerIsWorse(ShieldView.BondMargin));
            Assert.False(ShieldMapData.LowerIsWorse(ShieldView.SubstrateTemp));
        }
    }

    public class HostGeometryTests
    {
        private const string Host = @"
RTPS_HOST
{
    part = TestShield
    hostClass = rigid_disc
    partRole = dedicated
    area = 1.2
    substrateMaxTemp = 1100
    materials = test-ablator
}";

        private static HostDefinition Parse(string cfg, out List<ValidationIssue> issues)
        {
            issues = new List<ValidationIssue>();
            return HostDefinition.Parse(CfgParser.Parse(cfg).Nodes[0], issues);
        }

        [Fact]
        public void DefaultsAreTheStockGridAndTheUndersideOfThePart()
        {
            List<ValidationIssue> issues;
            HostDefinition host = Parse(Host, out issues);
            Assert.NotNull(host);
            Assert.Equal(new ShieldLayout(8, 2, 2.0), host.Layout());
            Assert.Equal(new[] { 0.0, -1.0, 0.0 }, host.ProtectedFace);
            Assert.Equal(new[] { 1.0, 0.0, 0.0 }, host.SectorZero);
            Assert.Equal("dedicated", host.PartRole);
            Assert.Equal(20.0, host.BackConductance);
            Assert.Equal(0.0, host.StructureLimitTemp);

            MaterialDefinition m = TestMaterials.Ablator();
            ShieldConstruction c = host.Construction(m, 0.55);
            Assert.Equal(1.2, c.Area);
            Assert.Equal(m.DesignThickness, c.LayerThickness);
            Assert.Equal(1100.0, c.CarrierLimitTemp);
            Assert.Equal(0.55, c.BareEmissivity);
            Assert.Null(c.Problem());
            Assert.Equal(ShieldConstruction.DefaultBareEmissivity, host.Construction(m, 0.0).BareEmissivity);
        }

        [Fact]
        public void GeometryFieldsAreRead()
        {
            List<ValidationIssue> issues;
            HostDefinition host = Parse(Host.Replace("area = 1.2", "area = 1.2\n    sectors = 12\n    rings = 3\n    centerPercent = 0\n    protectedFace = 0, 0, 1\n    sectorZero = 0, 1, 0\n    backConductance = 8\n    substrateHeatCapacity = 2000\n    structureLimitTemp = 450\n    deployAnimation = Unfold\n    deployedRadius = 5\n    thicknessFactor = 0.5"), out issues);
            Assert.NotNull(host);
            Assert.Equal(new ShieldLayout(12, 3, 0.0), host.Layout());
            Assert.Equal(new[] { 0.0, 0.0, 1.0 }, host.ProtectedFace);
            Assert.Equal(8.0, host.BackConductance);
            Assert.Equal(2000.0, host.SubstrateHeatCapacity);
            Assert.Equal(450.0, host.StructureLimitTemp);
            Assert.Equal("Unfold", host.DeployAnimation);
            Assert.Equal(5.0, host.DeployedRadius);
            Assert.Equal(TestMaterials.Tile().DesignThickness * 0.5, host.Construction(TestMaterials.Tile(), 0.4).LayerThickness, 12);
        }

        [Theory]
        [InlineData("partRole = dedicated", "partRole = shield")]
        [InlineData("partRole = dedicated", "partRol = dedicated")]
        [InlineData("area = 1.2", "area = 1.2\n    sectors = 3")]
        [InlineData("area = 1.2", "area = 1.2\n    sectors = 8.5")]
        [InlineData("area = 1.2", "area = 1.2\n    rings = 9")]
        [InlineData("area = 1.2", "area = 1.2\n    centerPercent = 140")]
        [InlineData("area = 1.2", "area = 1.2\n    protectedFace = 0, 0, 0")]
        [InlineData("area = 1.2", "area = 1.2\n    protectedFace = down")]
        [InlineData("area = 1.2", "area = 1.2\n    sectorZero = 0, -1, 0")]
        [InlineData("area = 1.2", "area = 1.2\n    backConductance = 0")]
        [InlineData("area = 1.2", "area = 1.2\n    substrateHeatCapacity = 5")]
        [InlineData("area = 1.2", "area = 1.2\n    structureLimitTemp = 100")]
        [InlineData("area = 1.2", "area = 1.2\n    deployAnimation = Unfold")]
        [InlineData("area = 1.2", "area = 1.2\n    deployedRadius = 5")]
        public void InvalidGeometryIsRefused(string find, string replace)
        {
            Assert.Contains(find, Host);
            List<ValidationIssue> issues;
            Assert.Null(Parse(Host.Replace(find, replace), out issues));
            Assert.Contains(issues, i => i.IsError);
        }
    }
}
