using System;
using System.Collections.Generic;
using RealTPS.Core.Shield;
using UnityEngine;

namespace RealTPS
{
    /// <summary>
    /// Where engine jets meet one shield. Each firing nozzle of a loaded vessel nearby casts one ray along its
    /// exhaust, as far as the engine's exhaust-damage range; when the first thing it meets (other than the
    /// engine itself) is this shield, the hit point is projected on the protected face and becomes a footprint
    /// weighted by thrust over distance squared.
    ///
    /// Nothing here adds heat. The game already puts jet heat into the part; these weights only say where on
    /// the face it lands (<see cref="ShieldWeights.Plume"/>).
    /// </summary>
    internal sealed class ShieldPlumes
    {
        private const float Interval = 0.25f;
        private const int MaxNozzles = 256;
        private const float MaxRange = 1000f;
        private const float SearchRadiusSquared = 1000f * 1000f;

        private readonly RaycastHit[] hits = new RaycastHit[48];
        private readonly List<PlumeHit> found = new List<PlumeHit>();
        private double[] weights = new double[0];
        private float nextTrace = float.NegativeInfinity;
        private bool usable;

        /// <summary>Number of nozzles whose jet met the shield at the last trace.</summary>
        public int Hits { get; private set; }

        /// <summary>
        /// Mean exhaust direction of the jets that met the shield at the last trace, world space, unit length,
        /// weighted like the footprint; zero when none did. Heat that goes through a breach follows it.
        /// </summary>
        public Vector3 Direction { get; private set; }

        // Summed in double: a weak, distant jet has a tiny weight that Unity's normalisation would round to zero.
        private double sumX, sumY, sumZ;

        /// <summary>
        /// The jet weights of the face (area-weighted mean 1), or null when no jet meets the shield. Traces at
        /// most every <see cref="Interval"/> seconds of game time.
        /// </summary>
        public double[] Weights(Part target, FaceFrame frame, double faceRadius, ShieldLayout layout)
        {
            if (weights.Length < layout.Count)
            {
                weights = new double[layout.Count];
                nextTrace = float.NegativeInfinity;
            }
            float now = Time.fixedTime;
            if (now >= nextTrace || now < nextTrace - 2f * Interval)
            {
                nextTrace = now + Interval;
                Trace(target, frame, faceRadius);
                usable = ShieldWeights.Plume(layout, found, weights);
            }
            return usable ? weights : null;
        }

        /// <summary>Forgets the last trace (the shield stopped stepping).</summary>
        public void Reset()
        {
            nextTrace = float.NegativeInfinity;
            usable = false;
            Hits = 0;
            Direction = Vector3.zero;
        }

        private void Trace(Part target, FaceFrame frame, double faceRadius)
        {
            found.Clear();
            Hits = 0;
            sumX = sumY = sumZ = 0.0;
            TraceAll(target, frame, faceRadius);
            double length = Math.Sqrt(sumX * sumX + sumY * sumY + sumZ * sumZ);
            Direction = length > 0.0 && !double.IsNaN(length) && !double.IsInfinity(length)
                ? new Vector3((float)(sumX / length), (float)(sumY / length), (float)(sumZ / length))
                : Vector3.zero;
        }

        private void TraceAll(Part target, FaceFrame frame, double faceRadius)
        {
            if (target == null || !(faceRadius > 0.0))
            {
                return;
            }
            Vector3 origin = target.transform.position;
            int nozzles = 0;
            List<Vessel> vessels = FlightGlobals.Vessels;
            for (int v = 0; v < vessels.Count; v++)
            {
                Vessel vessel = vessels[v];
                if (vessel == null || !vessel.loaded || vessel.packed || vessel.parts == null)
                {
                    continue;
                }
                for (int p = 0; p < vessel.parts.Count; p++)
                {
                    Part source = vessel.parts[p];
                    if (source == null || source == target || (source.transform.position - origin).sqrMagnitude > SearchRadiusSquared)
                    {
                        continue;
                    }
                    for (int m = 0; m < source.Modules.Count; m++)
                    {
                        ModuleEngines engine = source.Modules[m] as ModuleEngines;
                        if (engine == null || !engine.isEnabled || !engine.moduleIsEnabled || !engine.EngineIgnited
                            || !engine.exhaustDamage || !(engine.finalThrust > 0f) || engine.thrustTransforms == null)
                        {
                            continue;
                        }
                        for (int n = 0; n < engine.thrustTransforms.Count; n++)
                        {
                            if (++nozzles > MaxNozzles)
                            {
                                return;
                            }
                            Transform nozzle = engine.thrustTransforms[n];
                            if (nozzle == null)
                            {
                                continue;
                            }
                            float multiplier = engine.thrustTransformMultipliers != null && n < engine.thrustTransformMultipliers.Count
                                ? engine.thrustTransformMultipliers[n]
                                : 1f;
                            TraceNozzle(target, source, nozzle, Mathf.Clamp(engine.exhaustDamageMaxRange, 0f, MaxRange), engine.finalThrust * multiplier, frame, faceRadius);
                        }
                    }
                }
            }
        }

        private void TraceNozzle(Part target, Part source, Transform nozzle, float range, double thrust, FaceFrame frame, double faceRadius)
        {
            if (!(range > 0f) || !(thrust > 0.0))
            {
                return;
            }
            int count = Physics.RaycastNonAlloc(nozzle.position, nozzle.forward, hits, range, ~0, QueryTriggerInteraction.Ignore);
            int nearest = -1;
            float distance = float.MaxValue;
            Part nearestPart = null;
            for (int h = 0; h < count; h++)
            {
                if (hits[h].collider == null || hits[h].distance >= distance)
                {
                    continue;
                }
                Part owner = hits[h].collider.GetComponentInParent<Part>();
                if (owner == source)
                {
                    continue; // the engine's own colliders do not block its jet
                }
                nearest = h;
                distance = hits[h].distance;
                nearestPart = owner;
            }
            if (nearest < 0 || nearestPart != target)
            {
                return; // missed, or something else is in the way
            }

            Vector3 local = target.transform.InverseTransformPoint(hits[nearest].point);
            double x = (local.x * frame.Tx + local.y * frame.Ty + local.z * frame.Tz) / faceRadius;
            double y = (local.x * frame.Bx + local.y * frame.By + local.z * frame.Bz) / faceRadius;
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
            {
                return;
            }
            double strength = Math.Max(1.0e-9, thrust) / Math.Max(0.01, (double)distance * distance);
            found.Add(new PlumeHit
            {
                X = x,
                Y = y,
                Strength = strength,
                Spread = ShieldWeights.PlumeSpread(distance, faceRadius)
            });
            Vector3 exhaust = nozzle.forward.normalized;
            sumX += exhaust.x * strength;
            sumY += exhaust.y * strength;
            sumZ += exhaust.z * strength;
            Hits++;
        }
    }
}
