using System.Collections.Generic;
using RealTPS.Core.Cfg;
using RealTPS.Core.Materials;

namespace RealTPS.Core.Tests
{
    /// <summary>Hand-written fixtures. They are test data, not catalogue values.</summary>
    internal static class TestMaterials
    {
        public const string AblatorCfg = @"
RTPS_MATERIAL
{
    schemaVersion = 1
    id = test-ablator
    family = rigid_ablative
    density = 500
    designThickness = 0.05
    specificHeat = 1500
    emissivity = 0.7
    maxSurfaceTemp = 2500
    ABLATION
    {
        resource = RtpsTestAblator
        onsetTemp = 600
        activationTemp = 8000
        rateCoefficient = 5
        heatOfAblation = 5.0e6
    }
}";

        public static MaterialDefinition Parse(string cfg)
        {
            var issues = new List<ValidationIssue>();
            MaterialDefinition m = MaterialParser.Parse(CfgParser.Parse(cfg).Nodes[0], issues);
            if (m == null)
            {
                throw new System.InvalidOperationException("fixture did not parse: " + string.Join("; ", issues));
            }
            return m;
        }

        public static MaterialDefinition Ablator()
        {
            return Parse(AblatorCfg);
        }
    }
}
