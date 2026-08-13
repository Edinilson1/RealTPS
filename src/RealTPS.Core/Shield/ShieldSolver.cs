using System;

namespace RealTPS.Core.Shield
{
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

        /// <summary>Finite and strictly positive.</summary>
        private static bool Usable(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
        }
    }
}
