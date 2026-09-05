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
}
