using System;
using System.Globalization;
using RealTPS.Core.Cfg;
using RealTPS.Core.Physics;

namespace RealTPS.Core.Materials
{
    /// <summary>
    /// Serialises a material profile back to a <c>RTPS_MATERIAL</c> node. A vessel in flight stores this
    /// snapshot so that its shield keeps the exact profile it launched with even if the profile is later
    /// edited, hidden or uninstalled. <see cref="MaterialParser.Parse"/> reads it back.
    /// </summary>
    public static class MaterialWriter
    {
        public static CfgNode Write(MaterialDefinition m)
        {
            if (m == null)
            {
                throw new ArgumentNullException(nameof(m));
            }

            var node = new CfgNode(MaterialParser.NodeName);
            node.AddValue("schemaVersion", m.SchemaVersion.ToString(CultureInfo.InvariantCulture));
            node.AddValue("id", m.Id);
            node.AddValue("displayName", m.DisplayName ?? m.Id);
            node.AddValue("family", MaterialParser.FamilyToken(m.Family));
            if (m.Speculative)
            {
                node.AddValue("speculative", "true");
            }
            node.AddValue("permittedHostClasses", string.Join(", ", m.PermittedHostClasses.ToArray()));
            if (m.RequiredHostCapabilities.Count > 0)
            {
                node.AddValue("requiredHostCapabilities", string.Join(", ", m.RequiredHostCapabilities.ToArray()));
            }
            node.AddValue("density", F(m.Density));
            node.AddValue("designThickness", F(m.DesignThickness));
            node.AddValue("dryArealDensity", F(m.DryArealDensity));
            node.AddValue("specificHeat", F(m.SpecificHeat));
            node.AddValue("emissivity", F(m.Emissivity));
            node.AddValue("charEmissivity", F(m.CharEmissivity));
            node.AddValue("charSurfaceFraction", F(m.CharSurfaceFraction));
            node.AddValue("maxSurfaceTemp", F(m.MaxSurfaceTemp));
            node.AddValue("reuseSurfaceTemp", F(m.ReuseSurfaceTemp));
            node.AddValue("damageSurfaceTemp", F(m.DamageSurfaceTemp));
            node.AddValue("conductivity", F(m.Conductivity));
            node.AddValue("bondLimitTemp", F(m.BondLimitTemp));
            node.AddValue("costPerKg", F(m.CostPerKg));
            node.AddValue("costPerArea", F(m.CostPerArea));
            node.AddValue("unlockCost", F(m.UnlockCost));
            if (!string.IsNullOrEmpty(m.TechNode))
            {
                node.AddValue("techNode", m.TechNode);
            }

            if (m.Ablation != null)
            {
                CfgNode a = node.AddNode("ABLATION");
                a.AddValue("resource", m.Ablation.Resource);
                a.AddValue("onsetTemp", F(m.Ablation.OnsetTemp));
                a.AddValue("activationTemp", F(m.Ablation.ActivationTemp));
                a.AddValue("rateCoefficient", F(m.Ablation.RateCoefficient));
                a.AddValue("heatOfAblation", F(m.Ablation.HeatOfAblation));
            }
            if (m.Erosion != null)
            {
                CfgNode e = node.AddNode("EROSION");
                e.AddValue("resource", m.Erosion.Resource);
                e.AddValue("shearLimit", F(m.Erosion.ShearLimit));
                e.AddValue("shearRate", F(m.Erosion.ShearRate));
                e.AddValue("oxidationOnsetTemp", F(m.Erosion.OxidationOnsetTemp));
                e.AddValue("oxidationActivationTemp", F(m.Erosion.OxidationActivationTemp));
                e.AddValue("oxidationRate", F(m.Erosion.OxidationRate));
            }
            return node;
        }

        private static string F(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Builds the profile used for a vessel that was launched before RealTPS was installed and still carries the
    /// stock generic <c>Ablator</c>. It reproduces the stock rate (<c>0.1 * exp(-7500 / T) * maxAmount</c>
    /// per second) and sink (2.4 MJ/kg) with the part's original surface properties. It is not identical to
    /// stock: RealTPS's per-step energy bound applies and the rate is expressed per protected area. It states no
    /// conductivity and no bond limit, so it has no layer model and takes no damage.
    /// </summary>
    public static class LegacyProfile
    {
        public const string Id = "legacy-stock-ablator";
        public const string Resource = "Ablator";
        public const string HostClass = "legacy_host";

        public const double StockLossConst = 0.1;
        public const double StockLossExp = 7500.0;
        public const double StockThreshold = 500.0;

        /// <param name="area">Protected area of the host, m^2.</param>
        /// <param name="legacyCapacity">Stock ablator capacity of the legacy part, kg.</param>
        /// <param name="emissivity">The part's original emissive constant.</param>
        /// <param name="maxSurfaceTemp">The part's original skin temperature limit, K.</param>
        public static MaterialDefinition Create(double area, double legacyCapacity, double emissivity, double maxSurfaceTemp)
        {
            if (!(area > 0.0) || !(legacyCapacity > 0.0) || double.IsInfinity(area) || double.IsInfinity(legacyCapacity))
            {
                throw new ArgumentException("area and legacyCapacity must be positive and finite");
            }
            double eps = emissivity > 0.0 && emissivity <= 1.0 ? emissivity : 0.4;
            double limit = maxSurfaceTemp > StockThreshold ? maxSurfaceTemp : 2000.0;

            var m = new MaterialDefinition
            {
                Id = Id,
                DisplayName = Id,
                Family = ConstructionType.RigidAblative,
                Density = 1000.0,
                DesignThickness = legacyCapacity / (1000.0 * area),
                SpecificHeat = 400.0,
                Emissivity = eps,
                CharEmissivity = eps,
                MaxSurfaceTemp = limit,
                Ablation = new AblationChannel
                {
                    Resource = Resource,
                    OnsetTemp = StockThreshold,
                    ActivationTemp = StockLossExp,
                    RateCoefficient = StockLossConst * legacyCapacity / area,
                    HeatOfAblation = InventoryTransfer.StockHeatOfAblation
                }
            };
            m.PermittedHostClasses.Add(HostClass);
            return m;
        }
    }
}
