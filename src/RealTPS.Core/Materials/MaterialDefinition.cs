using System.Collections.Generic;

namespace RealTPS.Core.Materials
{
    /// <summary>
    /// Construction family. Its only rule is about the <c>ABLATION</c> channel (material consumed to cool the
    /// part): <c>rigid_ablative</c> materials must declare one, and the other families may not. Which hosts can
    /// carry a material is decided by its <c>permittedHostClasses</c>, not by its family.
    /// </summary>
    public enum ConstructionType
    {
        RigidAblative,
        RigidReusable,
        FlexibleInsulating,
        DeployableRadiative
    }

    /// <summary>Consumable channel with an energy sink (pyrolysis / ablation).</summary>
    public sealed class AblationChannel
    {
        /// <summary>KSP resource holding the remaining virgin material. 1 unit must weigh 1 kg.</summary>
        public string Resource;

        /// <summary>Skin temperature above which pyrolysis proceeds, K.</summary>
        public double OnsetTemp;

        /// <summary>Arrhenius activation temperature B in exp(-B/T), K.</summary>
        public double ActivationTemp;

        /// <summary>Pre-exponential areal mass-loss rate, kg/(m^2 s).</summary>
        public double RateCoefficient;

        /// <summary>Energy removed from the exposed skin per kilogram pyrolysed, J/kg. Excludes reradiation.</summary>
        public double HeatOfAblation;

    }

    /// <summary>Zero-sink mass-loss channels: mechanical removal and oxidation. They never cool the part.</summary>
    public sealed class ErosionChannel
    {
        /// <summary>Resource consumed. For ablators this equals the ablation resource.</summary>
        public string Resource;

        /// <summary>Dynamic pressure above which char/fabric is mechanically removed, Pa. 0 disables.</summary>
        public double ShearLimit;

        /// <summary>Areal loss rate at twice the shear limit, kg/(m^2 s).</summary>
        public double ShearRate;

        /// <summary>Skin temperature above which oxidation proceeds in an oxygen atmosphere, K. 0 disables.</summary>
        public double OxidationOnsetTemp;

        /// <summary>Arrhenius activation temperature for oxidation, K.</summary>
        public double OxidationActivationTemp;

        /// <summary>Pre-exponential areal oxidation rate, kg/(m^2 s).</summary>
        public double OxidationRate;
    }

    /// <summary>
    /// A validated thermal-protection material profile. All quantities are SI. Instances are immutable
    /// after parsing by convention; the solver only reads them.
    /// </summary>
    public sealed class MaterialDefinition
    {
        /// <summary>Schema written by this build, and assumed for a catalogue profile that states none.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>True when this build can read a profile of that schema version.</summary>
        public static bool IsSupportedSchema(int version)
        {
            return version == 1;
        }

        public int SchemaVersion = CurrentSchemaVersion;
        public string Id;
        public string DisplayName;
        public string Description;
        public ConstructionType Family;

        /// <summary>
        /// True for a theoretical or fictional material (<c>speculative = true</c>). The optional
        /// <c>GameData/RealTPSNoSpeculative</c> folder removes it, and it cannot be a baseline: it needs a techNode.
        /// </summary>
        public bool Speculative;

        /// <summary>Host classes this material may be assigned to. Never interpreted as a wildcard.</summary>
        public List<string> PermittedHostClasses = new List<string>();

        /// <summary>Host capabilities this material needs (for example <c>hot_structure</c>).</summary>
        public List<string> RequiredHostCapabilities = new List<string>();

        /// <summary>Virgin bulk density, kg/m^3.</summary>
        public double Density;

        /// <summary>Design thickness of the consumable or insulating layer, m.</summary>
        public double DesignThickness;

        /// <summary>Non-consumable mass per unit protected area (bond, carrier, coating, blanket), kg/m^2.</summary>
        public double DryArealDensity;

        /// <summary>Specific heat of the virgin material, J/(kg K). Informational; mirrors the resource hsp.</summary>
        public double SpecificHeat;

        public double Emissivity;

        /// <summary>Emissivity once the surface has charred. Equals <see cref="Emissivity"/> for non-charring materials.</summary>
        public double CharEmissivity;

        /// <summary>Pyrolysed fraction of the inventory at which the surface counts as fully charred, 0..1.</summary>
        public double CharSurfaceFraction = 0.05;

        /// <summary>
        /// Native catastrophic fallback, K: the skin temperature at which the stock engine destroys the part
        /// while the shield has not failed. The criterion behind the number (single-use limit, melting,
        /// burn-through, assumption) is recorded in the provenance. Damage starts lower and never reads it
        /// as a trigger.
        /// </summary>
        public double MaxSurfaceTemp;

        /// <summary>
        /// Surface temperature above which a reusable material is no longer reusable, K (0 = not stated).
        /// Reported once when exceeded; damage is governed by <see cref="DamageSurfaceTemp"/>.
        /// </summary>
        public double ReuseSurfaceTemp;

        /// <summary>Funds per kilogram of consumable (resource unit cost).</summary>
        public double CostPerKg;

        /// <summary>Funds per square metre for non-consumable construction.</summary>
        public double CostPerArea;

        public AblationChannel Ablation;
        public ErosionChannel Erosion;

        /// <summary>
        /// Tech node that unlocks the material in the stock tree; empty means "available with the host".
        /// Other tech trees replace it through a hand-written patch in TechTree/ (see <c>TechTreeOverrides</c>),
        /// never here.
        /// </summary>
        public string TechNode = string.Empty;

        /// <summary>Career entry cost of the unlock, funds.</summary>
        public double UnlockCost;

        /// <summary>
        /// True when the material carries a finite inventory: an ablator (cooling sink) or a wear layer that
        /// erosion/oxidation removes without any cooling benefit.
        /// </summary>
        public bool HasConsumable
        {
            get { return Ablation != null || Erosion != null; }
        }

        /// <summary>Resource holding the consumable inventory, or null.</summary>
        public string ConsumableResource
        {
            get { return Ablation != null ? Ablation.Resource : (Erosion != null ? Erosion.Resource : null); }
        }

        /// <summary>Consumable mass per unit protected area, kg/m^2 (0 for non-consumable materials).</summary>
        public double ConsumableArealDensity
        {
            get { return HasConsumable ? Density * DesignThickness : 0.0; }
        }

        /// <summary>Name of the unlock upgrade, or empty when the material needs none.</summary>
        public string UpgradeId
        {
            get { return string.IsNullOrEmpty(TechNode) ? string.Empty : "RTPS_" + Id.Replace('-', '_'); }
        }
    }
}
