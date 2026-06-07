using System;

namespace RealTPS.Core.Physics
{
    /// <summary>
    /// Pure rules for carrying a shield inventory across a change the player did not pay for in the editor:
    /// a legacy stock <c>Ablator</c> inventory found on an existing vessel, or a material swapped under a
    /// vessel in flight (locked subtype replaced, optional pack removed). Neither rule can add protection.
    /// </summary>
    public static class InventoryTransfer
    {
        /// <summary>Effective sink of the stock generic ablator: pyrolysisLossFactor (6000) * hsp (400) kJ/t, J/kg.</summary>
        public const double StockHeatOfAblation = 2.4e6;

        /// <summary>
        /// Converts a legacy inventory. The result is the smallest of: the same fill fraction, the same total
        /// sink energy, and the new capacity. Returns 0 for any invalid input.
        /// </summary>
        /// <param name="legacyAmount">Legacy inventory, kg.</param>
        /// <param name="legacyCapacity">Legacy capacity, kg.</param>
        /// <param name="legacyHeatOfAblation">Legacy sink per kg, J/kg.</param>
        /// <param name="newCapacity">Capacity of the new material on this host, kg.</param>
        /// <param name="newHeatOfAblation">Sink per kg of the new material, J/kg; 0 when it has no sink.</param>
        public static double ConvertLegacy(double legacyAmount, double legacyCapacity, double legacyHeatOfAblation, double newCapacity, double newHeatOfAblation)
        {
            if (!Positive(legacyAmount) || !Positive(legacyCapacity) || !Positive(newCapacity))
            {
                return 0.0;
            }

            double fraction = Math.Min(legacyAmount / legacyCapacity, 1.0);
            double result = newCapacity * fraction;

            if (Positive(legacyHeatOfAblation) && Positive(newHeatOfAblation))
            {
                double energyEquivalent = legacyAmount * legacyHeatOfAblation / newHeatOfAblation;
                result = Math.Min(result, energyEquivalent);
            }
            return Math.Min(result, newCapacity);
        }

        /// <summary>
        /// Inventory after an in-flight material change: the previous fill fraction applied to the new
        /// capacity, never more than what the host currently reports.
        /// </summary>
        public static double CarryFraction(double previousFraction, double newCapacity, double currentAmount)
        {
            if (double.IsNaN(previousFraction) || !Positive(newCapacity))
            {
                return 0.0;
            }
            double fraction = Math.Max(0.0, Math.Min(previousFraction, 1.0));
            double carried = newCapacity * fraction;
            if (!double.IsNaN(currentAmount) && currentAmount >= 0.0)
            {
                carried = Math.Min(carried, currentAmount);
            }
            return carried;
        }

        private static bool Positive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
        }
    }
}
