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
}
