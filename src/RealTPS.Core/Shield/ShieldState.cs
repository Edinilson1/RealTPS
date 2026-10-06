using System;
using System.Collections.Generic;
using System.Globalization;
using RealTPS.Core.Cfg;
using RealTPS.Core.Materials;

namespace RealTPS.Core.Shield
{
    /// <summary>
    /// The construction a shield was committed with: everything about the host that shapes the response of
    /// the protection. It is frozen in the saved state, so that a later change of the part config does not
    /// alter a shield in flight. All SI.
    /// </summary>
    public sealed class ShieldConstruction
    {
        /// <summary>Protected area, m^2.</summary>
        public double Area;

        /// <summary>Thickness of the layer of a material without a consumable inventory, m.</summary>
        public double LayerThickness;

        /// <summary>Conductance from the substrate to the part's interior, W/(m^2 K).</summary>
        public double BackConductance = DefaultBackConductance;

        /// <summary>Heat capacity of the substrate plate behind the protection, J/(m^2 K).</summary>
        public double SubstrateHeatCapacity = DefaultSubstrateHeatCapacity;

        /// <summary>Temperature limit of the structure the protection is bonded to, K. 0 = none stated.</summary>
        public double StructureLimitTemp;

        /// <summary>Surface limit of the bare carrier once the protection is gone, K.</summary>
        public double CarrierLimitTemp = DefaultCarrierLimitTemp;

        /// <summary>Emissivity of the bare carrier, shown by an exposed cell.</summary>
        public double BareEmissivity = DefaultBareEmissivity;

        public const double DefaultBackConductance = 20.0;

        /// <summary>About 3 mm of aluminium alloy.</summary>
        public const double DefaultSubstrateHeatCapacity = 7300.0;

        public const double DefaultCarrierLimitTemp = 1100.0;
        public const double DefaultBareEmissivity = 0.4;

        public ShieldConstruction Clone()
        {
            return (ShieldConstruction)MemberwiseClone();
        }

        /// <summary>The effective bond limit of a material on this construction, K; 0 when neither states one.</summary>
        public double BondLimit(MaterialDefinition material)
        {
            double bond = material != null ? material.BondLimitTemp : 0.0;
            if (bond > 0.0 && StructureLimitTemp > 0.0)
            {
                return Math.Min(bond, StructureLimitTemp);
            }
            return bond > 0.0 ? bond : StructureLimitTemp;
        }

        /// <summary>Null when every value is usable, otherwise what is wrong.</summary>
        public string Problem()
        {
            if (!Positive(Area))
            {
                return "area must be positive";
            }
            if (!(LayerThickness >= 0.0) || double.IsInfinity(LayerThickness))
            {
                return "layerThickness must be zero or positive";
            }
            if (!Positive(BackConductance))
            {
                return "backConductance must be positive";
            }
            if (!Positive(SubstrateHeatCapacity))
            {
                return "substrateHeatCapacity must be positive";
            }
            if (!(StructureLimitTemp >= 0.0) || double.IsInfinity(StructureLimitTemp))
            {
                return "structureLimitTemp must be zero or positive";
            }
            if (!Positive(CarrierLimitTemp))
            {
                return "carrierLimitTemp must be positive";
            }
            if (!(BareEmissivity > 0.0) || BareEmissivity > 1.0)
            {
                return "bareEmissivity must be in (0, 1]";
            }
            return null;
        }

        private static bool Positive(double value)
        {
            return value > 0.0 && !double.IsInfinity(value);
        }
    }

    /// <summary>
    /// Per-cell state of one shield. Inventory, capacity, pyrolysed mass and received energy are extensive
    /// (they add up over cells); integrity and temperatures are intensive. All SI.
    /// The grid and the construction are part of the state: both are frozen when the shield is committed.
    /// </summary>
    public sealed class ShieldState
    {
        public const string NodeName = "RTPS_SHIELD_STATE";
        public const int CurrentVersion = 1;

        /// <summary>Inventory below this counts as none, kg.</summary>
        public const double EmptyCell = 1.0e-9;

        public enum ReadStatus
        {
            Ok,
            Invalid,
            UnsupportedVersion
        }

        public readonly ShieldLayout Layout;
        public readonly ShieldConstruction Construction;

        /// <summary>Remaining consumable per cell, kg.</summary>
        public readonly double[] Inventory;

        /// <summary>Consumable per cell at commit, kg.</summary>
        public readonly double[] Capacity;

        /// <summary>Mass pyrolysed per cell so far, kg. Drives the local char emissivity.</summary>
        public readonly double[] Pyrolysed;

        /// <summary>Soundness of the protection in the cell, 1 = intact, 0 = gone.</summary>
        public readonly double[] Integrity;

        /// <summary>Estimated temperature of the protection layer, K.</summary>
        public readonly double[] LayerTemp;

        /// <summary>Estimated temperature of the substrate at the bond, K.</summary>
        public readonly double[] SubstrateTemp;

        /// <summary>False until the layer temperatures have been set from the part once.</summary>
        public bool ThermalInitialised;

        public ShieldState(ShieldLayout layout, ShieldConstruction construction)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }
            if (construction == null)
            {
                throw new ArgumentNullException(nameof(construction));
            }
            Layout = layout;
            Construction = construction;
            int n = layout.Count;
            Inventory = new double[n];
            Capacity = new double[n];
            Pyrolysed = new double[n];
            Integrity = new double[n];
            LayerTemp = new double[n];
            SubstrateTemp = new double[n];
            for (int i = 0; i < n; i++)
            {
                Integrity[i] = 1.0;
            }
        }

        public int Count
        {
            get { return Layout.Count; }
        }

        /// <summary>A new state with the totals spread over the cells by area.</summary>
        public static ShieldState Uniform(ShieldLayout layout, ShieldConstruction construction, double inventory, double capacity, double pyrolysed)
        {
            var state = new ShieldState(layout, construction);
            capacity = Sane(capacity);
            inventory = Math.Min(Sane(inventory), capacity);
            pyrolysed = Sane(pyrolysed);
            for (int i = 0; i < state.Count; i++)
            {
                double a = layout.AreaFraction(i);
                state.Capacity[i] = capacity * a;
                state.Inventory[i] = inventory * a;
                state.Pyrolysed[i] = pyrolysed * a;
            }
            return state;
        }

        public double TotalInventory
        {
            get { return Sum(Inventory); }
        }

        public double TotalCapacity
        {
            get { return Sum(Capacity); }
        }

        public double TotalPyrolysed
        {
            get { return Sum(Pyrolysed); }
        }

        /// <summary>Area-weighted mean integrity.</summary>
        public double MeanIntegrity
        {
            get
            {
                double sum = 0.0;
                for (int i = 0; i < Count; i++)
                {
                    sum += Integrity[i] * Layout.AreaFraction(i);
                }
                return sum;
            }
        }

        /// <summary>
        /// True when the cell no longer protects: the shield has failed, the cell's integrity is gone or, for a
        /// consumable material, its inventory is exhausted.
        /// </summary>
        public bool IsExposed(int cell, bool consumable)
        {
            return Integrity[cell] <= 0.0 || (consumable && Inventory[cell] <= EmptyCell);
        }

        /// <summary>Share of the face that is exposed, 0..1.</summary>
        public double ExposedArea(bool consumable)
        {
            double area = 0.0;
            for (int i = 0; i < Count; i++)
            {
                if (IsExposed(i, consumable))
                {
                    area += Layout.AreaFraction(i);
                }
            }
            return Math.Min(1.0, area);
        }

        /// <summary>Remaining share of the cell's consumable, 0..1; 1 when the cell never had one.</summary>
        public double Fraction(int cell)
        {
            return Capacity[cell] > 0.0 ? Math.Max(0.0, Math.Min(1.0, Inventory[cell] / Capacity[cell])) : 1.0;
        }

        /// <summary>Area of one cell, m^2.</summary>
        public double CellArea(int cell)
        {
            return Construction.Area * Layout.AreaFraction(cell);
        }

        /// <summary>
        /// Remaining thickness of the protection layer in a cell, m: from the inventory for a consumable
        /// material, the construction's layer thickness otherwise.
        /// </summary>
        public double Thickness(int cell, MaterialDefinition material)
        {
            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }
            if (!material.HasConsumable)
            {
                return Construction.LayerThickness;
            }
            double area = CellArea(cell);
            return area > 0.0 && material.Density > 0.0 ? Math.Max(0.0, Inventory[cell]) / (material.Density * area) : 0.0;
        }

        // ----- persistence -----

        public CfgNode Write()
        {
            var node = new CfgNode(NodeName);
            node.AddValue("version", CurrentVersion.ToString(CultureInfo.InvariantCulture));
            node.AddValue("layout", Layout.ToString());
            node.AddValue("area", R(Construction.Area));
            node.AddValue("layerThickness", R(Construction.LayerThickness));
            node.AddValue("backConductance", R(Construction.BackConductance));
            node.AddValue("substrateHeatCapacity", R(Construction.SubstrateHeatCapacity));
            node.AddValue("structureLimitTemp", R(Construction.StructureLimitTemp));
            node.AddValue("carrierLimitTemp", R(Construction.CarrierLimitTemp));
            node.AddValue("bareEmissivity", R(Construction.BareEmissivity));
            node.AddValue("inventory", Pack(Inventory));
            node.AddValue("capacity", Pack(Capacity));
            node.AddValue("pyrolysed", Pack(Pyrolysed));
            node.AddValue("integrity", Pack(Integrity));
            node.AddValue("layerTemp", Pack(LayerTemp));
            node.AddValue("substrateTemp", Pack(SubstrateTemp));
            node.AddValue("thermalInitialised", ThermalInitialised ? "true" : "false");
            return node;
        }

        /// <summary>
        /// Reads a stored state. Numerical safety only: every value must be finite and in its physical range.
        /// A state written by a newer build is refused, never guessed. The caller keeps a refused node and
        /// writes it back unchanged.
        /// </summary>
        public static ReadStatus Read(CfgNode node, List<ValidationIssue> issues, out ShieldState state)
        {
            state = null;
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }
            double version;
            if (!node.TryGetDouble("version", out version) || version < 1.0 || version != Math.Floor(version))
            {
                issues.Add(new ValidationIssue(true, "shield state", "missing or malformed version"));
                return ReadStatus.Invalid;
            }
            if (version > CurrentVersion)
            {
                issues.Add(new ValidationIssue(true, "shield state", "version " + version.ToString(CultureInfo.InvariantCulture) + " is newer than this build (" + CurrentVersion + ")"));
                return ReadStatus.UnsupportedVersion;
            }
            // State migrations go here, oldest first.

            ShieldLayout layout = ShieldLayout.Parse(node.GetValue("layout"));
            if (layout == null)
            {
                issues.Add(new ValidationIssue(true, "shield state", "invalid layout"));
                return ReadStatus.Invalid;
            }

            var construction = new ShieldConstruction();
            bool ok = Number(node, "area", out construction.Area, issues)
                & Number(node, "layerThickness", out construction.LayerThickness, issues)
                & Number(node, "backConductance", out construction.BackConductance, issues)
                & Number(node, "substrateHeatCapacity", out construction.SubstrateHeatCapacity, issues)
                & Number(node, "structureLimitTemp", out construction.StructureLimitTemp, issues)
                & Number(node, "carrierLimitTemp", out construction.CarrierLimitTemp, issues)
                & Number(node, "bareEmissivity", out construction.BareEmissivity, issues);
            if (ok)
            {
                string problem = construction.Problem();
                if (problem != null)
                {
                    issues.Add(new ValidationIssue(true, "shield state", problem));
                    ok = false;
                }
            }

            var result = new ShieldState(layout, construction);
            ok &= Unpack(node, "inventory", result.Inventory, 0.0, 1.0e12, issues)
                & Unpack(node, "capacity", result.Capacity, 0.0, 1.0e12, issues)
                & Unpack(node, "pyrolysed", result.Pyrolysed, 0.0, 1.0e12, issues)
                & Unpack(node, "integrity", result.Integrity, 0.0, 1.0, issues)
                & Unpack(node, "layerTemp", result.LayerTemp, 0.0, 1.0e5, issues)
                & Unpack(node, "substrateTemp", result.SubstrateTemp, 0.0, 1.0e5, issues);

            string initialised = (node.GetValue("thermalInitialised") ?? string.Empty).Trim();
            if (initialised != "true" && initialised != "false")
            {
                issues.Add(new ValidationIssue(true, "shield state", "thermalInitialised must be true or false"));
                ok = false;
            }
            if (!ok)
            {
                return ReadStatus.Invalid;
            }
            for (int i = 0; i < result.Count; i++)
            {
                if (result.Inventory[i] > result.Capacity[i] * (1.0 + 1.0e-9) + 1.0e-12)
                {
                    issues.Add(new ValidationIssue(true, "shield state", "cell " + layout.Label(i) + " holds more than its capacity"));
                    return ReadStatus.Invalid;
                }
                result.Inventory[i] = Math.Min(result.Inventory[i], result.Capacity[i]);
            }
            result.ThermalInitialised = initialised == "true";
            state = result;
            return ReadStatus.Ok;
        }

        private static bool Number(CfgNode node, string key, out double value, List<ValidationIssue> issues)
        {
            if (!node.TryGetDouble(key, out value) || double.IsNaN(value) || double.IsInfinity(value))
            {
                issues.Add(new ValidationIssue(true, "shield state", key + " is missing or not a finite number"));
                value = 0.0;
                return false;
            }
            return true;
        }

        private static bool Unpack(CfgNode node, string key, double[] target, double min, double max, List<ValidationIssue> issues)
        {
            string raw = node.GetValue(key);
            string[] parts = raw == null ? new string[0] : raw.Split(',');
            if (parts.Length != target.Length)
            {
                issues.Add(new ValidationIssue(true, "shield state", key + " must list " + target.Length + " values"));
                return false;
            }
            for (int i = 0; i < parts.Length; i++)
            {
                double value;
                if (!double.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                    || double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
                {
                    issues.Add(new ValidationIssue(true, "shield state", key + " holds an invalid value"));
                    return false;
                }
                target[i] = value;
            }
            return true;
        }

        private static string Pack(double[] values)
        {
            var parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                parts[i] = R(values[i]);
            }
            return string.Join(",", parts);
        }

        private static string R(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static double Sum(double[] values)
        {
            double sum = 0.0;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }
            return sum;
        }

        private static double Sane(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value < 0.0 ? 0.0 : value;
        }
    }
}
