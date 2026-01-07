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

    /// <summary>
    /// A validated thermal-protection material profile. All quantities are SI. Instances are immutable
    /// after parsing by convention; the solver only reads them.
    /// </summary>
    public sealed class MaterialDefinition
    {
        /// <summary>Schema written by this build, and assumed for a catalogue profile that states none.</summary>
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string Id;
        public string DisplayName;
        public string Description;
        public ConstructionType Family;

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

        /// <summary>Funds per kilogram of consumable (resource unit cost).</summary>
        public double CostPerKg;

        /// <summary>Funds per square metre for non-consumable construction.</summary>
        public double CostPerArea;

        public AblationChannel Ablation;

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
            get { return Ablation != null; }
        }

        /// <summary>Resource holding the consumable inventory, or null.</summary>
        public string ConsumableResource
        {
            get { return Ablation != null ? Ablation.Resource : null; }
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
