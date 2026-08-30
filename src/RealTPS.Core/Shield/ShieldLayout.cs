using System;
using System.Globalization;

namespace RealTPS.Core.Shield
{
    /// <summary>
    /// Division of a shield's protected face (a disc of unit radius) into cells: angular sectors in concentric
    /// bands of equal radial width, plus an optional centre disc. Cells are numbered from the innermost band
    /// outwards; the centre, when present, is the last cell. All areas are fractions of the face.
    /// A shield keeps the grid it was committed with: there is no remapping between grids.
    /// </summary>
    public sealed class ShieldLayout : IEquatable<ShieldLayout>
    {
        public const int MinSectors = 4;
        public const int MaxSectors = 40;
        public const int MaxRings = 6;
        public const int DefaultSectors = 8;
        public const int DefaultRings = 2;
        public const double DefaultCenterPercent = 2.0;

        /// <summary>Largest number of cells any layout can have.</summary>
        public const int MaxCells = MaxSectors * (MaxRings + 1) + 1;

        public readonly int Sectors;

        /// <summary>Number of circular cuts between the centre and the rim (bands = rings + 1).</summary>
        public readonly int Rings;

        /// <summary>Share of the face area taken by the centre disc, percent (0 = none, 100 = one whole-face cell).</summary>
        public readonly double CenterPercent;

        public readonly int Count;

        /// <summary>Index of the centre cell, or -1.</summary>
        public readonly int CenterIndex;

        private readonly double centerRadius;
        private readonly double[] inner;
        private readonly double[] outer;
        private readonly double[] area;
        private readonly int[][] neighbours;

        public ShieldLayout(int sectors, int rings, double centerPercent)
        {
            if (sectors < MinSectors || sectors > MaxSectors)
            {
                throw new ArgumentOutOfRangeException(nameof(sectors));
            }
            if (rings < 0 || rings > MaxRings)
            {
                throw new ArgumentOutOfRangeException(nameof(rings));
            }
            if (double.IsNaN(centerPercent) || centerPercent < 0.0 || centerPercent > 100.0)
            {
                throw new ArgumentOutOfRangeException(nameof(centerPercent));
            }

            centerPercent = Math.Round(centerPercent, 3, MidpointRounding.AwayFromZero);
            Sectors = sectors;
            Rings = rings;
            CenterPercent = centerPercent;
            centerRadius = Math.Sqrt(centerPercent / 100.0);

            int ringCells = centerPercent >= 100.0 ? 0 : sectors * (rings + 1);
            CenterIndex = centerPercent > 0.0 ? ringCells : -1;
            Count = ringCells + (CenterIndex >= 0 ? 1 : 0);

            inner = new double[Count];
            outer = new double[Count];
            area = new double[Count];
            neighbours = new int[Count][];

            for (int i = 0; i < ringCells; i++)
            {
                int band = i / sectors;
                int angular = i % sectors;
                inner[i] = centerRadius + (1.0 - centerRadius) * band / (rings + 1);
                outer[i] = centerRadius + (1.0 - centerRadius) * (band + 1) / (rings + 1);
                area[i] = (outer[i] * outer[i] - inner[i] * inner[i]) / sectors;

                bool hasInner = band > 0 || CenterIndex >= 0;
                bool hasOuter = band < rings;
                var adjacent = new int[2 + (hasInner ? 1 : 0) + (hasOuter ? 1 : 0)];
                adjacent[0] = band * sectors + (angular + sectors - 1) % sectors;
                adjacent[1] = band * sectors + (angular + 1) % sectors;
                int n = 2;
                if (band > 0)
                {
                    adjacent[n++] = i - sectors;
                }
                else if (CenterIndex >= 0)
                {
                    adjacent[n++] = CenterIndex;
                }
                if (hasOuter)
                {
                    adjacent[n] = i + sectors;
                }
                neighbours[i] = adjacent;
            }

            if (CenterIndex >= 0)
            {
                inner[CenterIndex] = 0.0;
                outer[CenterIndex] = centerRadius;
                area[CenterIndex] = centerPercent / 100.0;
                neighbours[CenterIndex] = new int[ringCells == 0 ? 0 : sectors];
                for (int i = 0; i < neighbours[CenterIndex].Length; i++)
                {
                    neighbours[CenterIndex][i] = i;
                }
            }
        }

        /// <summary>A single cell covering the whole face: the lumped model.</summary>
        public static ShieldLayout Single()
        {
            return new ShieldLayout(MinSectors, 0, 100.0);
        }

        public bool IsCenter(int cell)
        {
            return cell == CenterIndex;
        }

        public int Band(int cell)
        {
            return IsCenter(cell) ? -1 : cell / Sectors;
        }

        public int Sector(int cell)
        {
            return IsCenter(cell) ? -1 : cell % Sectors;
        }

        public double InnerRadius(int cell)
        {
            return inner[cell];
        }

        public double OuterRadius(int cell)
        {
            return outer[cell];
        }

        /// <summary>Area of the cell as a fraction of the face. The fractions sum to 1.</summary>
        public double AreaFraction(int cell)
        {
            return area[cell];
        }

        /// <summary>Angular position of the cell's middle, radians from sector zero; 0 for the centre.</summary>
        public double Angle(int cell)
        {
            return IsCenter(cell) ? 0.0 : 2.0 * Math.PI * Sector(cell) / Sectors;
        }

        /// <summary>Area-weighted mean radius of the cell (0 for the centre), in face radii.</summary>
        public double SampleRadius(int cell)
        {
            if (IsCenter(cell))
            {
                return 0.0;
            }
            double o = outer[cell];
            double n = inner[cell];
            return 2.0 / 3.0 * (o * o * o - n * n * n) / (o * o - n * n);
        }

        /// <summary>Cells sharing an edge with this one.</summary>
        public int[] Neighbours(int cell)
        {
            return neighbours[cell];
        }

        /// <summary>Display label: 1-based cell number, or "C" for the centre.</summary>
        public string Label(int cell)
        {
            return IsCenter(cell) ? "C" : (cell + 1).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Cell containing a point of the unit disc, or -1 outside it. <paramref name="x"/> points along sector
        /// zero and <paramref name="y"/> a quarter turn further in the direction of increasing sector number.
        /// </summary>
        public int CellAt(double x, double y)
        {
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
            {
                return -1;
            }
            double radius = Math.Sqrt(x * x + y * y);
            if (radius > 1.0)
            {
                return -1;
            }
            if (CenterIndex >= 0 && (radius < centerRadius || CenterPercent >= 100.0))
            {
                return CenterIndex;
            }
            double angle = Math.Atan2(y, x) + Math.PI / Sectors;
            if (angle < 0.0)
            {
                angle += 2.0 * Math.PI;
            }
            int sector = (int)Math.Floor(angle * Sectors / (2.0 * Math.PI)) % Sectors;
            int band = (int)Math.Floor((radius - centerRadius) / (1.0 - centerRadius) * (Rings + 1));
            band = Math.Max(0, Math.Min(Rings, band));
            return band * Sectors + sector;
        }

        /// <summary>Parses "sectors,rings,centerPercent". Returns null when the text is not a valid layout.</summary>
        public static ShieldLayout Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            string[] parts = text.Split(',');
            int sectors;
            int rings;
            double center;
            if (parts.Length != 3
                || !int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out sectors)
                || !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out rings)
                || !double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out center))
            {
                return null;
            }
            if (sectors < MinSectors || sectors > MaxSectors || rings < 0 || rings > MaxRings
                || double.IsNaN(center) || center < 0.0 || center > 100.0)
            {
                return null;
            }
            return new ShieldLayout(sectors, rings, center);
        }

        public override string ToString()
        {
            return Sectors.ToString(CultureInfo.InvariantCulture) + "," + Rings.ToString(CultureInfo.InvariantCulture) + ","
                + CenterPercent.ToString("R", CultureInfo.InvariantCulture);
        }

        public bool Equals(ShieldLayout other)
        {
            return other != null && Sectors == other.Sectors && Rings == other.Rings && CenterPercent == other.CenterPercent;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ShieldLayout);
        }

        public override int GetHashCode()
        {
            return Sectors ^ (Rings << 8) ^ CenterPercent.GetHashCode();
        }
    }
}
