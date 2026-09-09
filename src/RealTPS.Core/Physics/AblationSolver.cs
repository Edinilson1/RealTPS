using System;
using RealTPS.Core.Materials;

namespace RealTPS.Core.Physics
{
    /// <summary>Inputs of one solver step. All SI; the KSP adapter converts.</summary>
    public struct StepInput
    {
        /// <summary>Step length, s.</summary>
        public double Dt;

        /// <summary>Temperature of the exposed skin, K.</summary>
        public double SkinTemp;

        /// <summary>Heat capacity of the skin region the sink is applied to, J/K.</summary>
        public double SkinHeatCapacity;

        /// <summary>Protected (TPS) area of the host, m^2.</summary>
        public double Area;

        /// <summary>Remaining consumable inventory, kg.</summary>
        public double RemainingMass;

        /// <summary>Free-stream dynamic pressure, Pa.</summary>
        public double DynamicPressure;

        /// <summary>True when the surrounding atmosphere contains oxygen.</summary>
        public bool OxygenAtmosphere;
    }

    /// <summary>Result of one solver step. Every field is finite and non-negative.</summary>
    public struct StepResult
    {
        /// <summary>Inventory pyrolysed with an energy sink, kg.</summary>
        public double PyrolysedMass;

        /// <summary>Inventory removed by shear, kg (no sink).</summary>
        public double ShearErodedMass;

        /// <summary>Inventory removed by oxidation, kg (no sink).</summary>
        public double OxidisedMass;

        /// <summary>Energy removed from the skin during the step, J.</summary>
        public double HeatAbsorbed;

        /// <summary>True when the dynamic pressure exceeded the material's shear limit.</summary>
        public bool ShearOverload;

        /// <summary>Total inventory removed, kg.</summary>
        public double ConsumedMass
        {
            get { return PyrolysedMass + ShearErodedMass + OxidisedMass; }
        }

        /// <summary>Average sink power over the step, W. Zero for a zero-length step.</summary>
        public double SinkPower(double dt)
        {
            return dt > 0.0 ? HeatAbsorbed / dt : 0.0;
        }
    }

    /// <summary>
    /// Lumped thermal-protection material response: the rate laws of one whole face. RealTPS owns only the terms
    /// computed here: energy carried away by pyrolysed material and zero-sink erosion. Convection,
    /// reradiation, conduction and sensible heat storage belong to the host thermal engine and are never
    /// subtracted here. <c>Shield.ShieldSolver</c> applies the same laws cell by cell; with every cell stocked
    /// its totals equal this solver's.
    /// </summary>
    public static class AblationSolver
    {
        /// <summary>Largest step the rate law is integrated over; longer steps are treated as this long.</summary>
        public const double MaxStep = 1.0;

        /// <summary>Upper bound of the shear overload multiplier, so an absurd pressure cannot strip a shield in one frame.</summary>
        public const double MaxShearOverload = 10.0;

        /// <summary>
        /// Advances the material by one step. The energy bound applies to this sink in isolation: it guarantees
        /// that RealTPS alone never takes the skin below the onset temperature within a step; the host engine may
        /// still cool the skin further by radiation or conduction. Steps longer than <see cref="MaxStep"/> are
        /// deliberately truncated (less consumption and less cooling, never more). Invalid inputs (NaN, infinity, non-positive step, area or
        /// temperature) produce an all-zero result: the solver never invents cooling from bad data.
        /// </summary>
        public static StepResult Step(MaterialDefinition material, StepInput input)
        {
            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }

            var result = new StepResult();
            if (!IsUsable(input.Dt) || !IsUsable(input.SkinTemp) || !IsUsable(input.Area) || !IsUsable(input.RemainingMass))
            {
                return result;
            }

            double dt = Math.Min(input.Dt, MaxStep);
            double remaining = input.RemainingMass;

            AblationChannel ablation = material.Ablation;
            if (ablation != null && input.SkinTemp > ablation.OnsetTemp)
            {
                double arealRate = ablation.RateCoefficient * Math.Exp(-ablation.ActivationTemp / input.SkinTemp);
                double pyrolysed = arealRate * input.Area * dt;

                // No free cooling: the sink may not pull the skin below the onset temperature within one step.
                // Without a usable heat capacity the bound is unknown, so no sink is applied at all.
                double capacity = IsUsable(input.SkinHeatCapacity) ? input.SkinHeatCapacity : 0.0;
                double energyBudget = capacity * (input.SkinTemp - ablation.OnsetTemp);
                double budgetMass = energyBudget / ablation.HeatOfAblation;

                pyrolysed = Math.Min(pyrolysed, Math.Min(budgetMass, remaining));
                if (pyrolysed > 0.0 && !double.IsNaN(pyrolysed))
                {
                    result.PyrolysedMass = pyrolysed;
                    result.HeatAbsorbed = pyrolysed * ablation.HeatOfAblation;
                    remaining -= pyrolysed;
                }
            }

            ErosionChannel erosion = material.Erosion;
            if (erosion != null && remaining > 0.0)
            {
                if (erosion.ShearLimit > 0.0 && IsUsable(input.DynamicPressure) && input.DynamicPressure > erosion.ShearLimit)
                {
                    result.ShearOverload = true;
                    double overload = Math.Min(input.DynamicPressure / erosion.ShearLimit - 1.0, MaxShearOverload);
                    double sheared = Math.Min(erosion.ShearRate * overload * input.Area * dt, remaining);
                    if (sheared > 0.0)
                    {
                        result.ShearErodedMass = sheared;
                        remaining -= sheared;
                    }
                }

                if (erosion.OxidationOnsetTemp > 0.0 && input.OxygenAtmosphere && input.SkinTemp > erosion.OxidationOnsetTemp && remaining > 0.0)
                {
                    double rate = erosion.OxidationRate * Math.Exp(-erosion.OxidationActivationTemp / input.SkinTemp);
                    double oxidised = Math.Min(rate * input.Area * dt, remaining);
                    if (oxidised > 0.0 && !double.IsNaN(oxidised))
                    {
                        result.OxidisedMass = oxidised;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Surface emissivity for a given pyrolysed fraction of the design inventory: virgin value blending
        /// linearly to the char value once <see cref="MaterialDefinition.CharSurfaceFraction"/> has pyrolysed.
        /// Inventory removed by shear or oxidation must not be counted: stripping material makes no char.
        /// </summary>
        public static double SurfaceEmissivity(MaterialDefinition material, double pyrolysedFraction)
        {
            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }
            if (material.Ablation == null || double.IsNaN(pyrolysedFraction))
            {
                return material.Emissivity;
            }
            double t = Clamp01(pyrolysedFraction / material.CharSurfaceFraction);
            return material.Emissivity + (material.CharEmissivity - material.Emissivity) * t;
        }

        /// <summary>Inventory below this mass counts as exhausted, kg.</summary>
        public const double DepletionThreshold = 1.0e-6;

        /// <summary>Equivalent remaining thickness of the consumable layer, m.</summary>
        public static double EquivalentThickness(MaterialDefinition material, double remainingMass, double area)
        {
            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }
            if (!IsUsable(remainingMass) || !IsUsable(area) || material.Density <= 0.0)
            {
                return 0.0;
            }
            return remainingMass / (material.Density * area);
        }

        public static double Clamp01(double value)
        {
            if (double.IsNaN(value) || value <= 0.0)
            {
                return 0.0;
            }
            return value >= 1.0 ? 1.0 : value;
        }

        /// <summary>Finite and strictly positive.</summary>
        private static bool IsUsable(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
        }
    }
}
