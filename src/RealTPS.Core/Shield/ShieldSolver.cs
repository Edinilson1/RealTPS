using System;
using RealTPS.Core.Materials;
using RealTPS.Core.Physics;

namespace RealTPS.Core.Shield
{
    /// <summary>Inputs of one step of a shield. All SI; the KSP adapter converts.</summary>
    public struct ShieldStepInput
    {
        /// <summary>Step length, s.</summary>
        public double Dt;

        /// <summary>Temperature of the exposed skin, K.</summary>
        public double SkinTemp;

        /// <summary>Heat capacity of the exposed skin, J/K.</summary>
        public double ExposedSkinHeatCapacity;

        /// <summary>Free-stream dynamic pressure, Pa.</summary>
        public double DynamicPressure;

        /// <summary>True when the surrounding atmosphere contains oxygen.</summary>
        public bool OxygenAtmosphere;

        /// <summary>Convective power the engine put into the part, W. Zero or negative when it is not heating.</summary>
        public double ConvectivePower;

        /// <summary>How the flow meets the protected face.</summary>
        public Incidence Flow;
    }

    /// <summary>
    /// Result of one step. Created once per shield and reused, so that a physics step allocates nothing.
    /// Masses are kilograms, energies joules, times seconds.
    /// </summary>
    public sealed class ShieldStepResult
    {
        public double PyrolysedMass;
        public double ShearErodedMass;
        public double OxidisedMass;

        /// <summary>Energy removed from the exposed skin by pyrolysis, J. The single sink of the step.</summary>
        public double HeatAbsorbed;

        public bool ShearOverload;

        /// <summary>The weight modulation of the airflow in use, 0..1.</summary>
        public double Gate;

        /// <summary>
        /// Heat weight per cell, area-weighted mean 1: the gated airflow weights and the gated jet weights,
        /// blended in proportion to the convective and the jet power.
        /// </summary>
        public readonly double[] Weights;

        /// <summary>Gated windward factor per cell, area-weighted mean 1.</summary>
        public readonly double[] Windward;

        // Surface properties from the state at the end of the step; the engine sees them in its next step.

        /// <summary>Area-weighted emissivity of the face.</summary>
        public double Emissivity;

        /// <summary>Native catastrophic fallback, K.</summary>
        public double NativeLimit;

        /// <summary>Target conductance from skin to interior over the protected area, W/(m^2 K); 0 = none derived.</summary>
        public double Conductance;

        internal readonly double[] PyrolysisDemand;
        internal readonly double[] ShearDemand;
        internal readonly double[] OxidationDemand;

        public ShieldStepResult(int cells)
        {
            Weights = new double[cells];
            Windward = new double[cells];
            PyrolysisDemand = new double[cells];
            ShearDemand = new double[cells];
            OxidationDemand = new double[cells];
        }

        public double ConsumedMass
        {
            get { return PyrolysedMass + ShearErodedMass + OxidisedMass; }
        }

        internal void Reset(int cells)
        {
            PyrolysedMass = ShearErodedMass = OxidisedMass = HeatAbsorbed = Gate = 0.0;
            ShearOverload = false;
            for (int i = 0; i < cells; i++)
            {
                Weights[i] = 1.0;
                Windward[i] = 1.0;
            }
        }
    }

    /// <summary>
    /// Response of a shield, cell by cell (integration design I3-I14; design records kept outside the repository).
    /// RealTPS owns only what is computed here: the energy carried away by pyrolysed material, zero-sink erosion,
    /// an observer of the layer temperatures, damage, and the heat that exposed cells pass on. Convection,
    /// reradiation, conduction and sensible heat storage belong to the host thermal engine. The model has one
    /// surface temperature, the engine's exposed-skin node; the spatial weights allocate rates.
    /// </summary>
    public static class ShieldSolver
    {

        /// <summary>
        /// Advances a shield by one step. Invalid inputs (NaN, infinity, a non-positive step or temperature)
        /// change nothing: the solver never invents cooling or damage from bad data. Steps longer than
        /// <see cref="AblationSolver.MaxStep"/> are truncated.
        /// </summary>
        public static void Step(MaterialDefinition material, ShieldState state, ShieldStepInput input, ShieldStepResult result)
        {
            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            int n = state.Count;
            if (result.Weights.Length < n)
            {
                throw new ArgumentException("result was created for fewer cells than the state has");
            }
            result.Reset(n);

            if (!Usable(input.Dt) || !Usable(input.SkinTemp))
            {
                FillSurface(material, state, result);
                return;
            }

            ShieldLayout layout = state.Layout;
            ShieldConstruction construction = state.Construction;
            double dt = Math.Min(input.Dt, AblationSolver.MaxStep);
            double area = construction.Area;
            double capacity = Usable(input.ExposedSkinHeatCapacity) ? input.ExposedSkinHeatCapacity : 0.0;
            double skin = input.SkinTemp;
            double power = Usable(input.ConvectivePower) ? input.ConvectivePower : 0.0;
            bool consumable = material.HasConsumable;

            // ----- weights -----
            double[] w = result.Weights;
            double[] wind = result.Windward;
            double emissivity = Emissivity(material, state);
            ShieldWeights.Compute(layout, input.Flow.Defined ? input.Flow.Tilt : 0.0, input.Flow.Azimuth, w, wind);
            result.Gate = ShieldWeights.Gate(power, emissivity, skin, area, input.Flow.Defined ? input.Flow.Facing : 0.0);
            ShieldWeights.ApplyGate(w, n, result.Gate);
            ShieldWeights.ApplyGate(wind, n, result.Gate);

            // ----- consumption: one rate law at the skin temperature, allocated by weight, until each cell's first event -----
            if (consumable && !state.Failed && Usable(area))
            {
                Consume(material, state, input, result, dt, area, capacity, skin);
            }

            FillSurface(material, state, result);
        }

        /// <summary>
        /// Consumption of every cell over the part of the step before its first event (giving way, which the
        /// caller has put in <c>ProtectedFraction</c>). The channels run at constant rates over that window; a
        /// cell that empties consumes exactly what it had, shared between the channels in proportion to their
        /// rates, and is exposed from that moment. The pyrolysis rate is scaled by the largest common factor that
        /// keeps the sink inside its budget, found exactly (the sink is monotonic in that factor).
        /// </summary>
        private static void Consume(MaterialDefinition material, ShieldState state, ShieldStepInput input, ShieldStepResult result, double dt, double area, double capacity, double skin)
        {
            ShieldLayout layout = state.Layout;
            int n = state.Count;
            AblationChannel ablation = material.Ablation;
            ErosionChannel erosion = material.Erosion;
            double[] w = result.Weights;

            double pyrolysisBase = ablation != null && skin > ablation.OnsetTemp
                ? ablation.RateCoefficient * Math.Exp(-ablation.ActivationTemp / skin) * area * dt
                : 0.0;
            bool shearActive = erosion != null && erosion.ShearLimit > 0.0 && Usable(input.DynamicPressure);
            bool oxidising = erosion != null && erosion.OxidationOnsetTemp > 0.0 && input.OxygenAtmosphere && skin > erosion.OxidationOnsetTemp;
            double oxidationBase = oxidising ? erosion.OxidationRate * Math.Exp(-erosion.OxidationActivationTemp / skin) * area * dt : 0.0;

            for (int i = 0; i < n; i++)
            {
                double window = result.StartExposed[i] ? 0.0 : result.ProtectedFraction[i];
                result.PyrolysisDemand[i] = Sane(pyrolysisBase * layout.AreaFraction(i) * w[i] * window);
                result.OxidationDemand[i] = Sane(oxidationBase * layout.AreaFraction(i) * w[i] * window);
                double shear = 0.0;
                if (shearActive && window > 0.0)
                {
                    double local = input.DynamicPressure * result.Windward[i];
                    if (local > erosion.ShearLimit)
                    {
                        result.ShearOverload = true;
                        double overload = Math.Min(local / erosion.ShearLimit - 1.0, AblationSolver.MaxShearOverload);
                        shear = erosion.ShearRate * overload * area * layout.AreaFraction(i) * dt * window;
                    }
                }
                result.ShearDemand[i] = Sane(shear);
            }

            // No free cooling: the sink may not pull the skin below the onset temperature within one step.
            // Without a usable heat capacity the bound is unknown, so no sink is applied at all.
            double scale = 1.0;

            for (int i = 0; i < n; i++)
            {
                if (result.StartExposed[i])
                {
                    continue;
                }
                double inventory = state.Inventory[i];
                double pyrolysis = result.PyrolysisDemand[i] * scale;
                double shear = result.ShearDemand[i];
                double oxidation = result.OxidationDemand[i];
                double total = pyrolysis + shear + oxidation;
                double factor = 1.0;
                double remaining = inventory - total;
                double pyrolysed = pyrolysis * factor;
                state.Inventory[i] = remaining;
                state.Pyrolysed[i] += pyrolysed;
                result.PyrolysedMass += pyrolysed;
                result.ShearErodedMass += shear * factor;
                result.OxidisedMass += oxidation * factor;
                if (ablation != null)
                {
                    result.HeatAbsorbed += pyrolysed * ablation.HeatOfAblation;
                }
            }
        }

        private static double Sane(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value < 0.0 ? 0.0 : value;
        }

        // ----- surface properties and conduction -----

        /// <summary>
        /// Emissivity of the face: the area-weighted mean of the cells, each with its own char blend, an
        /// exposed cell showing the bare carrier. A profile without a layer model has no bare cells.
        /// </summary>
        public static double Emissivity(MaterialDefinition material, ShieldState state)
        {
            bool layered = material.HasLayerModel;
            bool consumable = material.HasConsumable;
            double sum = 0.0;
            for (int i = 0; i < state.Count; i++)
            {
                double value = layered && state.IsExposed(i, consumable)
                    ? state.Construction.BareEmissivity
                    : AblationSolver.SurfaceEmissivity(material, state.Capacity[i] > 0.0 ? state.Pyrolysed[i] / state.Capacity[i] : 0.0);
                sum += value * state.Layout.AreaFraction(i);
            }
            return sum;
        }

        /// <summary>
        /// The native catastrophic fallback: the material's while some cell still protects, the bare carrier's
        /// once the shield has failed or every cell is exposed.
        /// </summary>
        public static double NativeLimit(MaterialDefinition material, ShieldState state)
        {
            if (material.HasLayerModel && (state.Failed || state.ExposedArea(material.HasConsumable) >= 1.0 - 1.0e-9))
            {
                // Failed, or nothing left that protects: the bare carrier's limit.
                return Math.Min(material.MaxSurfaceTemp, state.Construction.CarrierLimitTemp);
            }
            return material.MaxSurfaceTemp;
        }

        /// <summary>
        /// Steady conductance from skin to interior over the protected area, W/(m^2 K): the cells in parallel,
        /// each a series of layer, contact and back resistances. 0 when the profile has no layer model.
        /// </summary>
        public static double Conductance(MaterialDefinition material, ShieldState state)
        {
            if (!material.HasLayerModel)
            {
                return 0.0;
            }
            bool consumable = material.HasConsumable;
            ShieldConstruction c = state.Construction;
            double sum = 0.0;
            for (int i = 0; i < state.Count; i++)
            {
                double thickness = state.IsExposed(i, consumable) ? 0.0 : state.Thickness(i, material);
                sum += state.Layout.AreaFraction(i) / (thickness / material.Conductivity + 1.0 / ContactConductance + 1.0 / c.BackConductance);
            }
            return sum;
        }

        /// <summary>
        /// The engine's own skin-to-interior conductance per unit of exposed skin, W/(m^2 K), with a conduction
        /// multiplier of 1: <c>1000 * conductionFactor * 10 * heatConductivity * 0.5 *
        /// skinInternalConductionFactor * convergenceFactor</c> (decompiled <c>FlightIntegrator</c>).
        /// </summary>
        public static double EngineConductance(double conductionFactor, double heatConductivity, double skinInternalConductionFactor, double convergenceFactor)
        {
            return 1000.0 * conductionFactor * 10.0 * heatConductivity * 0.5 * skinInternalConductionFactor * convergenceFactor;
        }

        public const double MinConductionMultiplier = 1.0e-4;
        public const double MaxConductionMultiplier = 100.0;

        /// <summary>
        /// The value of <c>skinInternalConductionMult</c> that makes the engine conduct like the target, or NaN
        /// when none can be derived (the caller then leaves the part's own multiplier in place). The protected
        /// area never counts for more than the exposed skin the engine has.
        /// </summary>
        public static double ConductionMultiplier(double targetConductance, double protectedArea, double nativeExposedArea, double engineConductance)
        {
            if (!Usable(targetConductance) || !Usable(protectedArea) || !Usable(nativeExposedArea) || !Usable(engineConductance))
            {
                return double.NaN;
            }
            double ratio = Math.Min(1.0, protectedArea / nativeExposedArea);
            double multiplier = targetConductance * ratio / engineConductance;
            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier))
            {
                return double.NaN;
            }
            return Math.Max(MinConductionMultiplier, Math.Min(MaxConductionMultiplier, multiplier));
        }

        private static void FillSurface(MaterialDefinition material, ShieldState state, ShieldStepResult result)
        {
            result.Emissivity = Emissivity(material, state);
            result.NativeLimit = NativeLimit(material, state);
            result.Conductance = Conductance(material, state);
        }

        /// <summary>Finite and strictly positive.</summary>
        private static bool Usable(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
        }
    }
}
