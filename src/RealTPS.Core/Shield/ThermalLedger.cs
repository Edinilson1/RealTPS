using System;
using System.Collections.Generic;

namespace RealTPS.Core.Shield
{
    /// <summary>
    /// Joules planned for each thermal reservoir of a vessel during one physics step, before anything is
    /// submitted to the engine. A reservoir is the exposed skin of a part or the interior of a part; the key
    /// type is the adapter's. Sinks are entered first, then transfers are arbitrated pair by pair on the
    /// temperatures projected through what is already planned, and everything is committed once.
    /// Energies are joules and capacities J/K: the adapter converts the engine's kJ/K at its boundary.
    /// </summary>
    public sealed class ThermalLedger<TKey>
    {
        /// <summary>Relaxation time of a transfer between two reservoirs, s. Calibration.</summary>
        public const double RelaxationTime = 0.4;

        private readonly Dictionary<TKey, double> net;

        public ThermalLedger(IEqualityComparer<TKey> comparer = null)
        {
            net = new Dictionary<TKey, double>(comparer ?? EqualityComparer<TKey>.Default);
        }

        public void Clear()
        {
            net.Clear();
        }

        /// <summary>Credits minus debits planned for the reservoir so far, J.</summary>
        public double Net(TKey reservoir)
        {
            double value;
            return net.TryGetValue(reservoir, out value) ? value : 0.0;
        }

        /// <summary>Plans a debit (a sink, or the giving side of a transfer). Negative or invalid amounts are ignored.</summary>
        public void Debit(TKey reservoir, double joules)
        {
            if (joules > 0.0 && !double.IsInfinity(joules))
            {
                net[reservoir] = Net(reservoir) - joules;
            }
        }

        /// <summary>Plans a credit. Negative or invalid amounts are ignored.</summary>
        public void Credit(TKey reservoir, double joules)
        {
            if (joules > 0.0 && !double.IsInfinity(joules))
            {
                net[reservoir] = Net(reservoir) + joules;
            }
        }

        /// <summary>Every reservoir with something planned, with its net joules. For the commit.</summary>
        public Dictionary<TKey, double>.Enumerator GetEnumerator()
        {
            return net.GetEnumerator();
        }

        /// <summary>Temperature of the reservoir once everything planned so far has been applied, K.</summary>
        public double Projected(TKey reservoir, double temperature, double capacity)
        {
            return Usable(capacity) ? temperature + Net(reservoir) / capacity : temperature;
        }

        /// <summary>
        /// The most a donor may pass to a recipient in a step, J: the share <c>1 - exp(-dt / tau)</c> of what
        /// would bring the two to the same temperature. The reduced capacity accounts for both temperatures
        /// moving, so a pair closes at most that share of its gap and never crosses.
        /// </summary>
        public static double PairBound(double donorCapacity, double donorTemp, double recipientCapacity, double recipientTemp, double dt)
        {
            if (!Usable(donorCapacity) || !Usable(recipientCapacity) || !Usable(dt)
                || double.IsNaN(donorTemp) || double.IsNaN(recipientTemp) || double.IsInfinity(donorTemp) || double.IsInfinity(recipientTemp))
            {
                return 0.0;
            }
            double gap = donorTemp - recipientTemp;
            if (!(gap > 0.0))
            {
                return 0.0;
            }
            double reduced = donorCapacity * recipientCapacity / (donorCapacity + recipientCapacity);
            return reduced * gap * (1.0 - Math.Exp(-dt / RelaxationTime));
        }

        /// <summary>
        /// Arbitrates one transfer and plans it. All requests between the same two reservoirs in a step must
        /// arrive here as one sum: the bound is per pair, not per request. Returns the joules granted.
        /// </summary>
        public double Transfer(TKey donor, double donorCapacity, double donorTemp, TKey recipient, double recipientCapacity, double recipientTemp, double requested, double dt)
        {
            if (!(requested > 0.0) || double.IsInfinity(requested))
            {
                return 0.0;
            }
            double bound = PairBound(donorCapacity, Projected(donor, donorTemp, donorCapacity), recipientCapacity, Projected(recipient, recipientTemp, recipientCapacity), dt);
            double granted = Math.Min(requested, bound);
            if (!(granted > 0.0))
            {
                return 0.0;
            }
            Debit(donor, granted);
            Credit(recipient, granted);
            return granted;
        }

        private static bool Usable(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
        }
    }

    /// <summary>
    /// Where the heat that exposed cells pass on goes, by the part's role. A failed shield always stays on the
    /// vessel and loses its protection (<see cref="ShieldFailure.ProtectionLost"/>); it never separates.
    /// </summary>
    public static class ShieldFailurePolicy
    {
        public const string RoleDedicated = "dedicated";
        public const string RoleMultifunction = "multifunction";

        /// <summary>
        /// True when heat passed on by exposed cells goes to the part's own interior instead of to the part
        /// found behind it.
        /// </summary>
        public static bool UsesOwnInterior(string role, bool hasCrewCapacity, bool hasCommand)
        {
            return role != RoleDedicated || hasCrewCapacity || hasCommand;
        }
    }
}
