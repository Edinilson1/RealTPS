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
