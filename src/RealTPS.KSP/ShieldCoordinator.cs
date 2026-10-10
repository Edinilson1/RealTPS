using System;
using System.Collections.Generic;
using RealTPS.Core.Shield;
using UnityEngine;

namespace RealTPS
{
    /// <summary>
    /// Runs the shields of one vessel once per physics step, as one transaction
    /// (integration design I13; design records kept outside the repository): (1) every shield plans its step; (2) the ledger is
    /// initialised with every shield's sink; (3) what exposed cells pass on is arbitrated pair by pair on the
    /// temperatures projected through the ledger; (4) each reservoir receives its net joules once;
    /// (5) surface properties for the engine's next step; (6) failures, after every thermal transaction.
    /// The first shield of a vessel whose <c>FixedUpdate</c> runs in a physics step drives the whole vessel.
    /// </summary>
    internal static class ShieldCoordinator
    {
        /// <summary>A thermal reservoir of a part: its exposed skin or its interior.</summary>
        internal struct Reservoir : IEquatable<Reservoir>
        {
            public readonly Part Part;
            public readonly bool Interior;

            public Reservoir(Part part, bool interior)
            {
                Part = part;
                Interior = interior;
            }

            public bool Equals(Reservoir other)
            {
                return ReferenceEquals(Part, other.Part) && Interior == other.Interior;
            }

            public override bool Equals(object obj)
            {
                return obj is Reservoir && Equals((Reservoir)obj);
            }

            public override int GetHashCode()
            {
                return (Part != null ? Part.GetInstanceID() * 2 : 0) + (Interior ? 1 : 0);
            }
        }

        private sealed class VesselShields
        {
            public readonly List<ModuleRtpsShield> Shields = new List<ModuleRtpsShield>();
            public readonly List<ModuleRtpsShield> Stepping = new List<ModuleRtpsShield>();
            public int PartCount = -1;
            public float NextRefresh;
            public double LastFixedTime = double.NaN;
            public FlightIntegrator Integrator;
        }

        private static readonly Dictionary<Vessel, VesselShields> Vessels = new Dictionary<Vessel, VesselShields>();
        private static readonly ThermalLedger<Reservoir> Ledger = new ThermalLedger<Reservoir>();
        private static float nextSweep;

        /// <summary>Heat capacity of a part's exposed skin, J/K. The engine keeps kJ/K: converted here.</summary>
        internal static double ExposedSkinCapacity(Part part)
        {
            if (part == null || !(part.skinExposedAreaFrac > 0.0))
            {
                return 0.0;
            }
            double recip = part.skinThermalMassRecip * part.skinExposedMassMult;
            if (double.IsNaN(recip) || double.IsInfinity(recip) || recip <= 0.0)
            {
                return 0.0;
            }
            return 1000.0 / recip;
        }

        /// <summary>Heat capacity of a part's interior, J/K.</summary>
        internal static double InteriorCapacity(Part part)
        {
            if (part == null || double.IsNaN(part.thermalMass) || double.IsInfinity(part.thermalMass) || part.thermalMass <= 0.0)
            {
                return 0.0;
            }
            return 1000.0 * part.thermalMass;
        }


        public static void Step(Vessel vessel)
        {
            if (vessel == null)
            {
                return;
            }
            Sweep();
            VesselShields entry;
            if (!Vessels.TryGetValue(vessel, out entry))
            {
                entry = new VesselShields();
                Vessels.Add(vessel, entry);
            }
            double now = Time.fixedTime;
            if (entry.LastFixedTime == now)
            {
                return;
            }
            entry.LastFixedTime = now;
            Refresh(vessel, entry);

            double dt = TimeWarp.fixedDeltaTime;
            if (entry.Integrator == null)
            {
                entry.Integrator = vessel.GetComponent<FlightIntegrator>();
            }
            // The integrator also goes analytic after a long gap between updates, not only in fast warp.
            bool analytic = entry.Integrator != null && entry.Integrator.isAnalytical;
            entry.Stepping.Clear();
            for (int i = 0; i < entry.Shields.Count; i++)
            {
                ModuleRtpsShield shield = entry.Shields[i];
                if (shield == null)
                {
                    continue;
                }
                if (shield.LastStepTime == now)
                {
                    // Already stepped in this physics tick (before a separation moved it to another vessel).
                    continue;
                }
                shield.LastStepTime = now;
                if (!(dt > 0.0) || analytic || !shield.ReadyToStep())
                {
                    shield.Idle();
                    continue;
                }
                try
                {
                    shield.Plan(dt);
                    entry.Stepping.Add(shield);
                }
                catch (Exception e)
                {
                    shield.Fault(e);
                }
            }
            if (entry.Stepping.Count == 0)
            {
                return;
            }

            bool failed = false;
            try
            {
                Ledger.Clear();
                for (int i = 0; i < entry.Stepping.Count; i++)
                {
                    ModuleRtpsShield shield = entry.Stepping[i];
                    Ledger.Debit(new Reservoir(shield.part, false), shield.PlannedSink);
                }
                for (int i = 0; i < entry.Stepping.Count; i++)
                {
                    ModuleRtpsShield shield = entry.Stepping[i];
                    try
                    {
                        shield.PlanTransfers(Ledger, dt);
                    }
                    catch (Exception e)
                    {
                        shield.Fault(e);
                    }
                }
                Commit(dt, entry.Stepping);
            }
            finally
            {
                Ledger.Clear();
            }

            for (int i = 0; i < entry.Stepping.Count; i++)
            {
                ModuleRtpsShield shield = entry.Stepping[i];
                try
                {
                    shield.Finish(dt);
                }
                catch (Exception e)
                {
                    shield.Fault(e);
                }
            }
            for (int i = 0; i < entry.Stepping.Count; i++)
            {
                ModuleRtpsShield shield = entry.Stepping[i];
                if (shield != null && shield.FailureDue)
                {
                    try
                    {
                        shield.EnactFailure();
                    }
                    catch (Exception e)
                    {
                        shield.Fault(e);
                    }
                    failed = true;
                }
            }
            if (failed)
            {
                // A separation changes the vessels: rebuild the list on the next step, but keep this tick's guard,
                // so that another shield's callback in the same tick does not run the vessel twice.
                entry.PartCount = -1;
                entry.Integrator = null;
            }
        }

        /// <summary>
        /// Each reservoir receives the net of its sink, debits and credits, once, in kW over the actual step. What
        /// a stepping shield's own exposed skin receives is remembered, so that its next step can tell the heat
        /// others added (engine jets) from its own submission.
        /// </summary>
        private static void Commit(double dt, List<ModuleRtpsShield> stepping)
        {
            for (int i = 0; i < stepping.Count; i++)
            {
                if (stepping[i] != null)
                {
                    stepping[i].CommittedExposedKw = 0.0;
                }
            }
            foreach (KeyValuePair<Reservoir, double> item in Ledger)
            {
                Part part = item.Key.Part;
                double joules = item.Value;
                if (part == null || joules == 0.0 || double.IsNaN(joules) || double.IsInfinity(joules))
                {
                    continue;
                }
                double kilowatts = joules * 0.001 / dt;
                if (item.Key.Interior)
                {
                    part.AddThermalFlux(kilowatts);
                }
                else
                {
                    part.AddExposedThermalFlux(kilowatts);
                    for (int i = 0; i < stepping.Count; i++)
                    {
                        if (stepping[i] != null && stepping[i].part == part)
                        {
                            stepping[i].CommittedExposedKw = kilowatts;
                        }
                    }
                }
            }
        }

        private static void Refresh(Vessel vessel, VesselShields entry)
        {
            if (vessel.parts == null)
            {
                entry.Shields.Clear();
                return;
            }
            if (entry.PartCount == vessel.parts.Count && Time.unscaledTime < entry.NextRefresh)
            {
                return;
            }
            entry.PartCount = vessel.parts.Count;
            entry.NextRefresh = Time.unscaledTime + 1.0f;
            entry.Shields.Clear();
            for (int p = 0; p < vessel.parts.Count; p++)
            {
                Part part = vessel.parts[p];
                if (part == null || part.Modules == null)
                {
                    continue;
                }
                for (int m = 0; m < part.Modules.Count; m++)
                {
                    var shield = part.Modules[m] as ModuleRtpsShield;
                    if (shield != null)
                    {
                        entry.Shields.Add(shield);
                    }
                }
            }
        }

        private static readonly List<Vessel> Stale = new List<Vessel>();

        private static void Sweep()
        {
            if (Time.unscaledTime < nextSweep)
            {
                return;
            }
            nextSweep = Time.unscaledTime + 10.0f;
            Stale.Clear();
            foreach (KeyValuePair<Vessel, VesselShields> item in Vessels)
            {
                if (item.Key == null || !item.Key.loaded)
                {
                    Stale.Add(item.Key);
                }
            }
            for (int i = 0; i < Stale.Count; i++)
            {
                Vessels.Remove(Stale[i]);
            }
            Stale.Clear();
        }
    }

    /// <summary>
    /// Finds, for each cell of a shield, the first other part of the same vessel that lies behind it along the
    /// direction the heat travels (the air relative to the craft, a jet's exhaust, or a blend of both). One ray
    /// per cell, from the cell's sample point just in front of the face. Hits on the shield itself are ignored; a ray that finds nothing gives no target.
    /// </summary>
    internal sealed class ShieldRouting
    {
        private const float RefreshInterval = 0.5f;
        private readonly Part[] targets;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private float nextRefresh;
        private double mappedRadius = -1.0;
        private Vector3 mappedAir;
        private Vessel mappedVessel;

        public ShieldRouting(int cells)
        {
            targets = new Part[cells];
        }

        public Part Target(int cell)
        {
            return cell >= 0 && cell < targets.Length ? targets[cell] : null;
        }

        public void Invalidate()
        {
            nextRefresh = 0f;
        }

        public void Refresh(Part shield, FaceFrame frame, ShieldLayout layout, Vector3 airMotion, double radius, bool force)
        {
            if (shield == null || layout == null || airMotion.sqrMagnitude < 1.0e-6f)
            {
                return;
            }
            bool turned = Vector3.Angle(airMotion, mappedAir) > 5.0f;
            if (!force && !turned && Time.unscaledTime < nextRefresh && mappedVessel == shield.vessel && Math.Abs(radius - mappedRadius) < 0.05)
            {
                return;
            }
            nextRefresh = Time.unscaledTime + RefreshInterval;
            mappedVessel = shield.vessel;
            mappedRadius = radius;
            mappedAir = airMotion;
            Array.Clear(targets, 0, targets.Length);

            Transform t = shield.transform;
            Vector3 normal = t.TransformDirection(new Vector3((float)frame.Nx, (float)frame.Ny, (float)frame.Nz)).normalized;
            Vector3 tangent = t.TransformDirection(new Vector3((float)frame.Tx, (float)frame.Ty, (float)frame.Tz)).normalized;
            Vector3 bitangent = t.TransformDirection(new Vector3((float)frame.Bx, (float)frame.By, (float)frame.Bz)).normalized;
            Vector3 centre = t.position;
            float depth = 0.25f;
            if (shield.collider != null)
            {
                Bounds bounds = shield.collider.bounds;
                centre = bounds.center;
                Vector3 e = bounds.extents;
                depth = Mathf.Abs(normal.x) * e.x + Mathf.Abs(normal.y) * e.y + Mathf.Abs(normal.z) * e.z;
            }
            Vector3 front = centre + normal * (depth + 0.02f);
            Vector3 direction = airMotion.normalized;
            float distance = Mathf.Clamp((float)radius * 12f, 5f, 100f);

            for (int i = 0; i < layout.Count && i < targets.Length; i++)
            {
                float r = (float)(layout.SampleRadius(i) * radius);
                float angle = (float)layout.Angle(i);
                Vector3 origin = front + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * r;
                targets[i] = FindTarget(shield, origin, direction, distance);
            }
        }

        private Part FindTarget(Part shield, Vector3 origin, Vector3 direction, float distance)
        {
            int n = Physics.RaycastNonAlloc(origin, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Part best = null;
            for (int h = 0; h < n; h++)
            {
                Collider collider = hits[h].collider;
                Part candidate = collider != null ? collider.GetComponentInParent<Part>() : null;
                if (candidate == null || candidate == shield || candidate.vessel != shield.vessel || candidate.isVesselEVA || hits[h].distance >= nearest)
                {
                    continue;
                }
                nearest = hits[h].distance;
                best = candidate;
            }
            return best;
        }
    }
}
