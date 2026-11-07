using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealTPS.Core.Shield
{
    /// <summary>
    /// Orthonormal frame of a protected face: outward normal, the direction of sector zero, and the direction
    /// a quarter turn further in the sense of increasing sector number (<c>normal x tangent</c>). Seen from
    /// outside, looking at the face, sector numbers increase counter-clockwise.
    /// </summary>
    public struct FaceFrame
    {
        public double Nx, Ny, Nz;
        public double Tx, Ty, Tz;
        public double Bx, By, Bz;
    }

    /// <summary>How the oncoming flow meets a protected face.</summary>
    public struct Incidence
    {
        /// <summary>Cosine of the angle between the face normal and the direction of travel: 1 = face first, 0 = edge on, negative = face in the lee.</summary>
        public double Facing;

        /// <summary>Sine of that angle, 0..1.</summary>
        public double Tilt;

        /// <summary>Angle on the face, radians from sector zero towards increasing sectors, of the side that leads into the flow.</summary>
        public double Azimuth;

        /// <summary>False when there is no usable flow direction; every other field is then zero.</summary>
        public bool Defined;
    }

    public static class ShieldGeometry
    {
        /// <summary>Parses "x, y, z".</summary>
        public static bool TryParseVector(string text, out double[] vector)
        {
            vector = null;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string[] parts = text.Split(',');
            if (parts.Length != 3)
            {
                return false;
            }
            var result = new double[3];
            for (int i = 0; i < 3; i++)
            {
                if (!double.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result[i])
                    || double.IsNaN(result[i]) || double.IsInfinity(result[i]))
                {
                    return false;
                }
            }
            vector = result;
            return true;
        }

        public static string FormatVector(double[] vector)
        {
            return vector[0].ToString("R", CultureInfo.InvariantCulture) + ", "
                + vector[1].ToString("R", CultureInfo.InvariantCulture) + ", "
                + vector[2].ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>True when the normal is a direction and the tangent is not parallel to it.</summary>
        public static bool IsUsableFrame(double[] normal, double[] tangent)
        {
            FaceFrame frame;
            return TryMakeFrame(normal, tangent, out frame);
        }

        /// <summary>Builds the face frame: the tangent is made perpendicular to the normal.</summary>
        public static bool TryMakeFrame(double[] normal, double[] tangent, out FaceFrame frame)
        {
            frame = new FaceFrame();
            if (normal == null || tangent == null || normal.Length != 3 || tangent.Length != 3)
            {
                return false;
            }
            double n = Math.Sqrt(normal[0] * normal[0] + normal[1] * normal[1] + normal[2] * normal[2]);
            if (!(n > 1.0e-9) || double.IsInfinity(n))
            {
                return false;
            }
            double nx = normal[0] / n, ny = normal[1] / n, nz = normal[2] / n;
            double along = tangent[0] * nx + tangent[1] * ny + tangent[2] * nz;
            double tx = tangent[0] - along * nx, ty = tangent[1] - along * ny, tz = tangent[2] - along * nz;
            double t = Math.Sqrt(tx * tx + ty * ty + tz * tz);
            if (!(t > 1.0e-6) || double.IsInfinity(t))
            {
                return false;
            }
            tx /= t;
            ty /= t;
            tz /= t;
            frame.Nx = nx;
            frame.Ny = ny;
            frame.Nz = nz;
            frame.Tx = tx;
            frame.Ty = ty;
            frame.Tz = tz;
            frame.Bx = ny * tz - nz * ty;
            frame.By = nz * tx - nx * tz;
            frame.Bz = nx * ty - ny * tx;
            return true;
        }

        /// <summary>
        /// Incidence of the flow on a face. <paramref name="vx"/>, <paramref name="vy"/>, <paramref name="vz"/>
        /// is the direction in which the craft moves through the air, in the coordinates of the frame; it need
        /// not be normalised.
        /// </summary>
        public static Incidence Incidence(FaceFrame frame, double vx, double vy, double vz)
        {
            var result = new Incidence();
            double speed = Math.Sqrt(vx * vx + vy * vy + vz * vz);
            if (!(speed > 1.0e-9) || double.IsInfinity(speed))
            {
                return result;
            }
            vx /= speed;
            vy /= speed;
            vz /= speed;
            double facing = vx * frame.Nx + vy * frame.Ny + vz * frame.Nz;
            double pt = vx * frame.Tx + vy * frame.Ty + vz * frame.Tz;
            double pb = vx * frame.Bx + vy * frame.By + vz * frame.Bz;
            double tilt = Math.Sqrt(pt * pt + pb * pb);
            result.Defined = true;
            result.Facing = Math.Max(-1.0, Math.Min(1.0, facing));
            result.Tilt = Math.Min(1.0, tilt);
            // The side of the face that lies further along the direction of travel meets the flow first.
            result.Azimuth = tilt > 1.0e-9 ? Math.Atan2(pb, pt) : 0.0;
            return result;
        }
    }

    /// <summary>Where the jet of one engine nozzle meets the shield, seen on the protected face.</summary>
    public struct PlumeHit
    {
        /// <summary>Face coordinates of the hit point, in face radii (x along sector zero, y a quarter turn on).</summary>
        public double X, Y;

        /// <summary>Relative strength of the jet (thrust share over distance squared). Only ratios between hits matter.</summary>
        public double Strength;

        /// <summary>Standard deviation of the footprint on the face, face radii.</summary>
        public double Spread;
    }

    /// <summary>
    /// Spatial weights of the cells of a face. They only distribute what the engine computes for the whole
    /// part: every array has an area-weighted mean of exactly 1. The profile is a gameplay calibration of a
    /// distribution the game does not compute (docs/wiki/How-it-works.md).
    /// </summary>
    public static class ShieldWeights
    {
        /// <summary>Radius of a jet footprint at the nozzle, m. Calibration.</summary>
        public const double PlumeSpreadBase = 0.15;

        /// <summary>Growth of the footprint radius per metre from the nozzle, m/m (about 6 degrees). Calibration.</summary>
        public const double PlumeSpreadPerMetre = 0.10;

        /// <summary>Narrowest footprint, face radii: keeps one hit from landing on a single sample point only.</summary>
        public const double MinPlumeSpread = 0.08;

        /// <summary>Footprint spread on a face of the given radius for a jet that travelled <paramref name="distance"/> m.</summary>
        public static double PlumeSpread(double distance, double faceRadius)
        {
            if (!(faceRadius > 0.0) || double.IsInfinity(faceRadius))
            {
                return 1.0;
            }
            double metres = PlumeSpreadBase + PlumeSpreadPerMetre * (distance > 0.0 && !double.IsInfinity(distance) ? distance : 0.0);
            return Math.Max(MinPlumeSpread, Math.Min(4.0, metres / faceRadius));
        }

        /// <summary>
        /// Weights of the heat that engine jets put on the face: a Gaussian footprint around each hit, summed by
        /// strength and normalised to an area-weighted mean of 1. A hit off the face still weights the cells
        /// nearest to it. Fills 1 everywhere and returns false when no hit is usable.
        /// </summary>
        public static bool Plume(ShieldLayout layout, IList<PlumeHit> hits, double[] weights)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }
            if (weights == null || weights.Length < layout.Count)
            {
                throw new ArgumentException("weight array must hold one value per cell");
            }
            int n = layout.Count;
            for (int i = 0; i < n; i++)
            {
                weights[i] = 0.0;
            }
            int count = hits != null ? hits.Count : 0;
            for (int h = 0; h < count; h++)
            {
                PlumeHit hit = hits[h];
                if (!(hit.Strength > 0.0) || double.IsInfinity(hit.Strength) || !Finite(hit.X) || !Finite(hit.Y))
                {
                    continue;
                }
                double spread = hit.Spread > MinPlumeSpread && !double.IsInfinity(hit.Spread) ? hit.Spread : MinPlumeSpread;
                double twoSigma2 = 2.0 * spread * spread;
                for (int i = 0; i < n; i++)
                {
                    double r = layout.SampleRadius(i);
                    double a = layout.Angle(i);
                    double dx = r * Math.Cos(a) - hit.X;
                    double dy = r * Math.Sin(a) - hit.Y;
                    weights[i] += hit.Strength * Math.Exp(-(dx * dx + dy * dy) / twoSigma2);
                }
            }
            double mean = 0.0;
            for (int i = 0; i < n; i++)
            {
                mean += weights[i] * layout.AreaFraction(i);
            }
            if (!(mean > 1.0e-300) || double.IsInfinity(mean))
            {
                for (int i = 0; i < n; i++)
                {
                    weights[i] = 1.0;
                }
                return false;
            }
            for (int i = 0; i < n; i++)
            {
                weights[i] /= mean;
            }
            return true;
        }

        private static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
        /// <summary>Relative drop of heating from the centre to the rim at zero incidence.</summary>
        public const double RadialFalloff = 0.10;

        /// <summary>Strength of the windward bias at full tilt.</summary>
        public const double TiltGain = 0.8;

        private const double StefanBoltzmann = 5.670374419e-8;

        /// <summary>
        /// Fills <paramref name="heat"/> (radial profile times windward bias) and <paramref name="windward"/>
        /// (windward bias alone). Both are normalised to an area-weighted mean of 1.
        /// </summary>
        public static void Compute(ShieldLayout layout, double tilt, double azimuth, double[] heat, double[] windward)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }
            if (heat == null || windward == null || heat.Length < layout.Count || windward.Length < layout.Count)
            {
                throw new ArgumentException("weight arrays must hold one value per cell");
            }
            if (double.IsNaN(tilt) || tilt < 0.0)
            {
                tilt = 0.0;
            }
            if (tilt > 1.0)
            {
                tilt = 1.0;
            }
            if (double.IsNaN(azimuth) || double.IsInfinity(azimuth))
            {
                azimuth = 0.0;
                tilt = 0.0;
            }

            double heatMean = 0.0;
            double windMean = 0.0;
            for (int i = 0; i < layout.Count; i++)
            {
                double r = layout.SampleRadius(i);
                double bias = 1.0 + TiltGain * tilt * r * Math.Cos(layout.Angle(i) - azimuth);
                windward[i] = bias;
                heat[i] = (1.0 - RadialFalloff * r * r) * bias;
                heatMean += heat[i] * layout.AreaFraction(i);
                windMean += bias * layout.AreaFraction(i);
            }
            for (int i = 0; i < layout.Count; i++)
            {
                heat[i] /= heatMean;
                windward[i] /= windMean;
            }
        }

        /// <summary>
        /// How much of the spatial modulation applies, 0..1: an activity heuristic (the whole part's
        /// convective power over what the protected face radiates) times a factor that removes all modulation
        /// when the face looks away from the flow.
        /// </summary>
        public static double Gate(double convectivePower, double emissivity, double skinTemp, double area, double facing)
        {
            if (double.IsNaN(convectivePower) || convectivePower <= 0.0 || double.IsNaN(facing) || facing <= 0.0)
            {
                return 0.0;
            }
            double radiated = emissivity * StefanBoltzmann * skinTemp * skinTemp * skinTemp * skinTemp * area;
            if (double.IsNaN(radiated) || double.IsInfinity(radiated) || radiated <= 0.0)
            {
                return 0.0;
            }
            double activity = Math.Min(1.0, convectivePower / radiated);
            return activity * Math.Min(1.0, 5.0 * facing);
        }

        /// <summary>Replaces each weight by <c>1 + gate * (w - 1)</c>. The mean stays 1.</summary>
        public static void ApplyGate(double[] weights, int count, double gate)
        {
            if (double.IsNaN(gate) || gate < 0.0)
            {
                gate = 0.0;
            }
            if (gate > 1.0)
            {
                gate = 1.0;
            }
            for (int i = 0; i < count; i++)
            {
                weights[i] = 1.0 + gate * (weights[i] - 1.0);
            }
        }
    }
}
