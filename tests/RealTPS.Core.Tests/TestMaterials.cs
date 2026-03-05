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
    permittedHostClasses = rigid_disc
    density = 500
    designThickness = 0.05
    specificHeat = 1500
    emissivity = 0.7
    charEmissivity = 0.9
    maxSurfaceTemp = 2500
    costPerKg = 2
    ABLATION
    {
        resource = RtpsTestAblator
        onsetTemp = 600
        activationTemp = 8000
        rateCoefficient = 5
        heatOfAblation = 5.0e6
    }
    EROSION
    {
        shearLimit = 50000
        shearRate = 0.02
    }
}";

        public const string CarbonCfg = @"
RTPS_MATERIAL
{
    id = test-carbon-fabric
    family = deployable_radiative
    permittedHostClasses = deployable_rib_fabric
    requiredHostCapabilities = hot_structure
    density = 900
    designThickness = 0.004
    specificHeat = 900
    emissivity = 0.9
    maxSurfaceTemp = 2400
    costPerKg = 50
    EROSION
    {
        resource = RtpsTestCarbonCloth
        oxidationOnsetTemp = 900
        oxidationActivationTemp = 9000
        oxidationRate = 3
    }
}";

        public const string TileCfg = @"
RTPS_MATERIAL
{
    schemaVersion = 1
    id = test-tile
    family = rigid_reusable
    permittedHostClasses = rigid_disc
    density = 144
    designThickness = 0.06
    dryArealDensity = 9
    specificHeat = 1000
    emissivity = 0.85
    maxSurfaceTemp = 1750
    costPerArea = 900
    techNode = hypersonicFlight
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

        public static MaterialDefinition Carbon()
        {
            return Parse(CarbonCfg);
        }

        public static MaterialDefinition Tile()
        {
            return Parse(TileCfg);
        }
    }
}
