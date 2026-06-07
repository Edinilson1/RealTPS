using System.Collections.Generic;
using RealTPS.Core.Cfg;
using RealTPS.Core.Materials;
using Xunit;

namespace RealTPS.Core.Tests
{
    public class SnapshotTests
    {
        private static MaterialParser.SnapshotStatus Read(MaterialDefinition original, out MaterialDefinition copy, System.Func<string, string> mutate = null)
        {
            string text = CfgParser.Write(MaterialWriter.Write(original));
            if (mutate != null)
            {
                text = mutate(text);
            }
            return MaterialParser.ParseSnapshot(CfgParser.Parse(text).Nodes[0], new List<ValidationIssue>(), out copy);
        }

        [Theory]
        [InlineData(1.0, 0.1)]        // thinner than any catalogue profile may be
        [InlineData(1.27, 200.0)]     // stock 1.25 m shield
        [InlineData(2.449, 20.0)]     // Making History pod
        [InlineData(1000.0, 0.001)]
        [InlineData(0.001, 100000.0)]
        public void LegacyProfileSnapshotRoundTripsOutsideCatalogueRanges(double area, double capacity)
        {
            MaterialDefinition legacy = LegacyProfile.Create(area, capacity, 0.4, 3300.0);
            MaterialDefinition copy;
            Assert.Equal(MaterialParser.SnapshotStatus.Ok, Read(legacy, out copy));
            Assert.Equal(legacy.Ablation.RateCoefficient, copy.Ablation.RateCoefficient);
            Assert.Equal(legacy.Ablation.HeatOfAblation, copy.Ablation.HeatOfAblation);
            Assert.Equal(legacy.DesignThickness, copy.DesignThickness);
            Assert.Equal("Ablator", copy.ConsumableResource);
            Assert.Equal(CfgParser.Write(MaterialWriter.Write(legacy)), CfgParser.Write(MaterialWriter.Write(copy)));

            // The same node is not admissible as a catalogue entry when it leaves the catalogue ranges.
            if (legacy.DesignThickness < 0.0005 || legacy.DesignThickness > 1.0)
            {
                var issues = new List<ValidationIssue>();
                Assert.Null(MaterialParser.Parse(MaterialWriter.Write(legacy), issues));
            }
        }

        [Fact]
        public void CatalogueProfilesRoundTripAsSnapshots()
        {
            foreach (MaterialDefinition original in new[] { TestMaterials.Ablator(), TestMaterials.Carbon(), TestMaterials.Tile() })
            {
                MaterialDefinition copy;
                Assert.Equal(MaterialParser.SnapshotStatus.Ok, Read(original, out copy));
                Assert.Equal(CfgParser.Write(MaterialWriter.Write(original)), CfgParser.Write(MaterialWriter.Write(copy)));
            }
        }

        [Fact]
        public void NewerSchemaIsReportedNotGuessed()
        {
            MaterialDefinition copy;
            Assert.Equal(MaterialParser.SnapshotStatus.UnsupportedVersion,
                Read(TestMaterials.Ablator(), out copy, t => t.Replace("schemaVersion = 1", "schemaVersion = 2")));
            Assert.Null(copy);
        }

        [Theory]
        [InlineData("schemaVersion = 1", "schemaVersion = 0")]
        [InlineData("schemaVersion = 1", "schemaVersion = 1.5")]
        [InlineData("schemaVersion = 1", "schemaVersion = abc")]
        [InlineData("heatOfAblation = 5000000", "heatOfAblation = NaN")]
        [InlineData("heatOfAblation = 5000000", "heatOfAblation = -1")]
        [InlineData("heatOfAblation = 5000000", "heatOfAblation = 0")]
        [InlineData("density = 500", "density = 0")]
        [InlineData("emissivity = 0.7", "emissivity = -0.1")]
        [InlineData("family = rigid_ablative", "family = unknown")]
        [InlineData("resource = RtpsTestAblator", "resource = ")]
        public void UnsafeSnapshotsAreInvalid(string find, string replace)
        {
            string baseline = CfgParser.Write(MaterialWriter.Write(TestMaterials.Ablator()));
            Assert.Contains(find, baseline);
            MaterialDefinition copy;
            Assert.Equal(MaterialParser.SnapshotStatus.Invalid, Read(TestMaterials.Ablator(), out copy, t => t.Replace(find, replace)));
            Assert.Null(copy);
        }

        [Fact]
        public void SnapshotKeepsValuesTheCatalogueWouldReject()
        {
            // A profile committed under older, wider limits must stay readable after the catalogue tightens.
            MaterialDefinition copy;
            Assert.Equal(MaterialParser.SnapshotStatus.Ok,
                Read(TestMaterials.Ablator(), out copy, t => t.Replace("designThickness = 0.05", "designThickness = 5")));
            Assert.Equal(5.0, copy.DesignThickness);
        }
    }
}
