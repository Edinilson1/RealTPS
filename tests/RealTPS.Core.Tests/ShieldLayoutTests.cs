using System;
using RealTPS.Core.Shield;
using Xunit;

namespace RealTPS.Core.Tests
{
    public class ShieldLayoutTests
    {
        [Theory]
        [InlineData(8, 2, 2.0, 25)]
        [InlineData(4, 0, 0.0, 4)]
        [InlineData(40, 6, 5.0, 281)]
        [InlineData(8, 2, 0.0, 24)]
        [InlineData(8, 2, 100.0, 1)]
        public void CellCountAndAreasAreConsistent(int sectors, int rings, double center, int expected)
        {
            var layout = new ShieldLayout(sectors, rings, center);
            Assert.Equal(expected, layout.Count);
            double sum = 0.0;
            for (int i = 0; i < layout.Count; i++)
            {
                Assert.True(layout.AreaFraction(i) > 0.0);
                Assert.True(layout.OuterRadius(i) > layout.InnerRadius(i));
                Assert.InRange(layout.SampleRadius(i), layout.InnerRadius(i), layout.OuterRadius(i));
                sum += layout.AreaFraction(i);
            }
            Assert.Equal(1.0, sum, 12);
            Assert.True(layout.Count <= ShieldLayout.MaxCells);
        }

        [Fact]
        public void SingleCellLayoutCoversTheWholeFace()
        {
            ShieldLayout layout = ShieldLayout.Single();
            Assert.Equal(1, layout.Count);
            Assert.True(layout.IsCenter(0));
            Assert.Equal(1.0, layout.AreaFraction(0), 12);
            Assert.Equal(0.0, layout.SampleRadius(0));
            Assert.Empty(layout.Neighbours(0));
            Assert.Equal(0, layout.CellAt(0.7, -0.2));
        }

        [Fact]
        public void NeighboursAreSymmetric()
        {
            var layout = new ShieldLayout(8, 2, 2.0);
            for (int i = 0; i < layout.Count; i++)
            {
                foreach (int j in layout.Neighbours(i))
                {
                    Assert.Contains(i, layout.Neighbours(j));
                }
            }
            // The centre touches every cell of the innermost band and nothing else.
            Assert.Equal(8, layout.Neighbours(layout.CenterIndex).Length);
        }

        [Fact]
        public void EveryCellContainsItsOwnSamplePoint()
        {
            foreach (ShieldLayout layout in new[] { new ShieldLayout(8, 2, 2.0), new ShieldLayout(5, 0, 0.0), new ShieldLayout(12, 6, 10.0) })
            {
                for (int i = 0; i < layout.Count; i++)
                {
                    double r = layout.SampleRadius(i);
                    double a = layout.Angle(i);
                    Assert.Equal(i, layout.CellAt(r * Math.Cos(a), r * Math.Sin(a)));
                }
                Assert.Equal(-1, layout.CellAt(1.01, 0.0));
                Assert.Equal(-1, layout.CellAt(double.NaN, 0.0));
            }
        }

        [Theory]
        [InlineData("8,2,2", 8, 2, 2.0)]
        [InlineData(" 12 , 0 , 0 ", 12, 0, 0.0)]
        public void ParsesAndRoundTrips(string text, int sectors, int rings, double center)
        {
            ShieldLayout layout = ShieldLayout.Parse(text);
            Assert.NotNull(layout);
            Assert.Equal(new ShieldLayout(sectors, rings, center), layout);
            Assert.Equal(layout, ShieldLayout.Parse(layout.ToString()));
        }

        [Theory]
        [InlineData("")]
        [InlineData("3,2,2")]
        [InlineData("41,2,2")]
        [InlineData("8,7,2")]
        [InlineData("8,2,101")]
        [InlineData("8,2,NaN")]
        [InlineData("8,2")]
        [InlineData("a,b,c")]
        public void InvalidLayoutsAreNotParsed(string text)
        {
            Assert.Null(ShieldLayout.Parse(text));
        }

        [Fact]
        public void LabelsNumberCellsFromOneAndNameTheCentre()
        {
            var layout = new ShieldLayout(8, 2, 2.0);
            Assert.Equal("1", layout.Label(0));
            Assert.Equal("24", layout.Label(23));
            Assert.Equal("C", layout.Label(layout.CenterIndex));
        }
    }
}
