using System;
using RealTPS.Core.Materials;

namespace RealTPS.Core.Shield
{
    /// <summary>
    /// Per-cell alerts, computed from the cell's own values. Each view marks only the alerts about what it
    /// measures (<see cref="ShieldMapData.Marks"/>); a marked cell is drawn flat and hard-edged in that view.
    /// (Drawing every alert in every view let a cell out of material hide its own temperature and integrity in
    /// the other views.)
    /// </summary>
    [Flags]
    public enum CellFlags
    {
        None = 0,
        Exposed = 1,
        CriticalIntegrity = 2,
        LowReserve = 4,
        BondAtRisk = 8
    }

    /// <summary>The quantity a map shows. All are intensive: a uniform layer draws as a uniform colour.</summary>
    public enum ShieldView
    {
        /// <summary>Remaining share of the consumable, 0..1.</summary>
        Reserve,

        /// <summary>Integrity of the protection, 0..1.</summary>
        Integrity,

        /// <summary>Estimated temperature of the substrate at the bond, K.</summary>
        SubstrateTemp,

        /// <summary>The cell's own bond limit minus its substrate temperature, K.</summary>
        BondMargin,

        /// <summary>Heat received so far per unit area, J/m^2.</summary>
        Received
    }

    /// <summary>Everything the display needs from a shield, read in one go. Reused between refreshes.</summary>
    public sealed class ShieldMapData
    {
        /// <summary>Integrity below this is critical (display threshold).</summary>
        public const double CriticalIntegrity = 0.2;

        /// <summary>A consumable cell with less than this share left is low (display threshold).</summary>
        public const double LowReserve = 0.10;

        /// <summary>A bond margin at or below this is at risk, K: where bond degradation starts to run.</summary>
        public const double BondRiskMargin = ShieldSolver.BondRampStart;

        public const int ViewCount = 5;

        public ShieldLayout Layout { get; private set; }
        public int Count { get; private set; }

        /// <summary>False when the profile has no layer model (legacy vessels): there is no damage state to show.</summary>
        public bool HasDamageState { get; private set; }

        public bool Consumable { get; private set; }

        /// <summary>False when neither the material nor the host states a bond limit.</summary>
        public bool HasBondLimit { get; private set; }

        public readonly CellFlags[] Flags;
        private readonly double[][] values;

        public double ExposedArea { get; private set; }
        public int ExposedCells { get; private set; }
        public int CriticalCells { get; private set; }
        public int LowReserveCells { get; private set; }
        public int BondRiskCells { get; private set; }
        public double Structure { get; private set; }
        public ShieldFailure Failure { get; private set; }

        /// <summary>Smallest bond margin of any cell, K; NaN when no bond limit is stated.</summary>
        public double SmallestBondMargin { get; private set; }

        public ShieldMapData(int maxCells)
        {
            Flags = new CellFlags[maxCells];
            values = new double[ViewCount][];
            for (int v = 0; v < ViewCount; v++)
            {
                values[v] = new double[maxCells];
            }
        }

        /// <summary>Per-cell values of a view. NaN = not available for that cell.</summary>
        public double[] Values(ShieldView view)
        {
            return values[(int)view];
        }

        /// <summary>
        /// The alerts a view marks: only those about what it measures. Reserve: exposed and low reserve;
        /// integrity: critical integrity; bond temperature and margin: bond at risk; received heat: none.
        /// </summary>
        public static CellFlags Marks(ShieldView view)
        {
            switch (view)
            {
                case ShieldView.Reserve:
                    return CellFlags.Exposed | CellFlags.LowReserve;
                case ShieldView.Integrity:
                    return CellFlags.CriticalIntegrity;
                case ShieldView.SubstrateTemp:
                case ShieldView.BondMargin:
                    return CellFlags.BondAtRisk;
                default:
                    return CellFlags.None;
            }
        }

        /// <summary>True when lower values of the view are worse.</summary>
        public static bool LowerIsWorse(ShieldView view)
        {
            return view == ShieldView.Reserve || view == ShieldView.Integrity || view == ShieldView.BondMargin;
        }

        /// <summary>
        /// The worst cell of a view: the minimum for reserve, integrity and bond margin, the maximum for the
        /// temperature and the received heat. -1 when the view has no value on any cell.
        /// </summary>
        public int WorstCell(ShieldView view)
        {
            double[] v = Values(view);
            bool lower = LowerIsWorse(view);
            int worst = -1;
            for (int i = 0; i < Count; i++)
            {
                if (double.IsNaN(v[i]))
                {
                    continue;
                }
                if (worst < 0 || (lower ? v[i] < v[worst] : v[i] > v[worst]))
                {
                    worst = i;
                }
            }
            return worst;
        }

        public void Fill(MaterialDefinition material, ShieldState state, ShieldSettings settings)
        {
            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (state.Count > Flags.Length)
            {
                throw new ArgumentException("map data was created for fewer cells than the state has");
            }
            settings = settings ?? ShieldSettings.Default;
            Layout = state.Layout;
            Count = state.Count;
            Consumable = material.HasConsumable;
            HasDamageState = material.HasLayerModel;
            double bondLimit = state.Construction.BondLimit(material);
            HasBondLimit = HasDamageState && bondLimit > 0.0;
            Structure = state.Structure;
            Failure = state.Failure;
            ExposedCells = CriticalCells = LowReserveCells = BondRiskCells = 0;
            ExposedArea = HasDamageState ? state.ExposedArea(Consumable) : 0.0;
            SmallestBondMargin = double.NaN;

            for (int i = 0; i < Count; i++)
            {
                bool exposed = HasDamageState && state.IsExposed(i, Consumable);
                double reserve = Consumable ? state.Fraction(i) : double.NaN;
                double integrity = HasDamageState ? (exposed && state.Failed ? 0.0 : state.Integrity[i]) : double.NaN;
                double substrate = HasDamageState && state.ThermalInitialised ? state.SubstrateTemp[i] : double.NaN;
                // The margin uses the cell's own limit: limits may differ between cells.
                double margin = HasBondLimit && !double.IsNaN(substrate)
                    ? bondLimit * ShieldSolver.CellBias(state.Seed, i, settings.CellVariation) - substrate
                    : double.NaN;
                double cellArea = state.CellArea(i);

                values[(int)ShieldView.Reserve][i] = exposed && Consumable ? 0.0 : reserve;
                values[(int)ShieldView.Integrity][i] = integrity;
                values[(int)ShieldView.SubstrateTemp][i] = substrate;
                values[(int)ShieldView.BondMargin][i] = margin;
                values[(int)ShieldView.Received][i] = cellArea > 0.0 ? state.Received[i] / cellArea : double.NaN;

                // An exposed cell is counted once, as exposed. It still carries the critical-integrity flag when
                // its integrity is gone (a shed cell, a broken tile), so that the integrity view marks it too.
                CellFlags flags = CellFlags.None;
                if (HasDamageState && integrity < CriticalIntegrity)
                {
                    flags |= CellFlags.CriticalIntegrity;
                    if (!exposed)
                    {
                        CriticalCells++;
                    }
                }
                if (exposed)
                {
                    flags |= CellFlags.Exposed;
                    ExposedCells++;
                }
                else
                {
                    if (Consumable && reserve < LowReserve)
                    {
                        flags |= CellFlags.LowReserve;
                        LowReserveCells++;
                    }
                    // At or below the threshold, any negative margin included: the flag never clears by getting worse.
                    if (!double.IsNaN(margin) && margin <= BondRiskMargin)
                    {
                        flags |= CellFlags.BondAtRisk;
                        BondRiskCells++;
                    }
                }
                Flags[i] = flags;
                if (!double.IsNaN(margin) && !exposed && (double.IsNaN(SmallestBondMargin) || margin < SmallestBondMargin))
                {
                    SmallestBondMargin = margin;
                }
            }
        }
    }

    /// <summary>
    /// The smoothed field of a view over the face. Values are interpolated between cell sample points, linear
    /// in radius and in azimuth; a flagged cell is flat and gives nothing to its neighbours; nothing is
    /// extrapolated, so the field never shows a value more extreme than one a cell holds.
    /// Face coordinates: x along sector zero, y a quarter turn further towards increasing sector numbers.
    /// </summary>
    public static class ShieldField
    {
        /// <summary>Every alert counts (a cell with any flag is flat).</summary>
        public const CellFlags AllMarks = CellFlags.Exposed | CellFlags.CriticalIntegrity | CellFlags.LowReserve | CellFlags.BondAtRisk;

        /// <summary>The value at a point of the unit disc; NaN outside it or where the view has no value.</summary>
        public static double Sample(ShieldLayout layout, double[] values, CellFlags[] flags, double x, double y)
        {
            return Sample(layout, values, flags, AllMarks, x, y);
        }

        /// <summary>
        /// The value at a point of the unit disc, where only the alerts in <paramref name="marks"/> make a cell
        /// flat (<see cref="ShieldMapData.Marks"/> for a view); NaN outside the disc or where there is no value.
        /// </summary>
        public static double Sample(ShieldLayout layout, double[] values, CellFlags[] flags, CellFlags marks, double x, double y)
        {
            int own = layout.CellAt(x, y);
            if (own < 0)
            {
                return double.NaN;
            }
            double ownValue = values[own];
            if (double.IsNaN(ownValue) || (flags[own] & marks) != CellFlags.None || layout.Count == 1)
            {
                return ownValue;
            }

            int sectors = layout.Sectors;
            int bands = layout.Rings + 1;
            bool centre = layout.CenterIndex >= 0;
            double radius = Math.Sqrt(x * x + y * y);
            double turn = Math.Atan2(y, x) / (2.0 * Math.PI);
            if (turn < 0.0)
            {
                turn += 1.0;
            }
            double s = turn * sectors;
            int s0 = (int)Math.Floor(s) % sectors;
            int s1 = (s0 + 1) % sectors;
            double ts = s - Math.Floor(s);

            // Radial bracket between the sample radii of adjacent bands; the centre cell is the pole.
            int inner = -1;          // band index of the inner node; -1 = the centre cell
            int outer = 0;
            double tr = 1.0;
            double first = layout.SampleRadius(0);
            if (radius <= first)
            {
                if (centre && first > 0.0)
                {
                    tr = radius / first;
                }
                else
                {
                    inner = 0;
                    tr = 0.0;
                }
            }
            else
            {
                inner = bands - 1;
                outer = bands - 1;
                tr = 0.0;
                for (int b = 1; b < bands; b++)
                {
                    double hi = layout.SampleRadius(b * sectors);
                    if (radius <= hi)
                    {
                        double lo = layout.SampleRadius((b - 1) * sectors);
                        inner = b - 1;
                        outer = b;
                        tr = hi > lo ? (radius - lo) / (hi - lo) : 0.0;
                        break;
                    }
                }
            }

            double sum = 0.0;
            double weight = 0.0;
            if (inner < 0)
            {
                Add(values, flags, marks, layout.CenterIndex, 1.0 - tr, ref sum, ref weight);
            }
            else
            {
                Add(values, flags, marks, inner * sectors + s0, (1.0 - tr) * (1.0 - ts), ref sum, ref weight);
                Add(values, flags, marks, inner * sectors + s1, (1.0 - tr) * ts, ref sum, ref weight);
            }
            if (tr > 0.0)
            {
                Add(values, flags, marks, outer * sectors + s0, tr * (1.0 - ts), ref sum, ref weight);
                Add(values, flags, marks, outer * sectors + s1, tr * ts, ref sum, ref weight);
            }
            return weight > 1.0e-12 ? sum / weight : ownValue;
        }

        private static void Add(double[] values, CellFlags[] flags, CellFlags marks, int cell, double w, ref double sum, ref double weight)
        {
            if (w <= 0.0 || (flags[cell] & marks) != CellFlags.None || double.IsNaN(values[cell]))
            {
                return;
            }
            sum += values[cell] * w;
            weight += w;
        }
    }

    /// <summary>Fixed colour scale of a view. It does not rescale to the current range.</summary>
    public struct MapScale
    {
        public double Min;
        public double Max;

        /// <summary>True when <see cref="Min"/> is the bad end.</summary>
        public bool LowIsBad;

        /// <summary>The fixed scale of a view. Temperatures are scaled by the shield's own bond limit.</summary>
        public static MapScale For(ShieldView view, double bondLimit)
        {
            switch (view)
            {
                case ShieldView.Reserve:
                case ShieldView.Integrity:
                    return new MapScale { Min = 0.0, Max = 1.0, LowIsBad = true };
                case ShieldView.SubstrateTemp:
                    return new MapScale { Min = 250.0, Max = bondLimit > 300.0 ? bondLimit : 600.0, LowIsBad = false };
                case ShieldView.BondMargin:
                    return new MapScale { Min = 0.0, Max = 300.0, LowIsBad = true };
                default:
                    return new MapScale { Min = 0.0, Max = 60.0e6, LowIsBad = false };
            }
        }

        /// <summary>1 = the good end of the scale, 0 = the bad end.</summary>
        public double Goodness(double value)
        {
            double t = Max > Min ? (value - Min) / (Max - Min) : 0.0;
            if (double.IsNaN(t))
            {
                return 0.0;
            }
            t = t < 0.0 ? 0.0 : (t > 1.0 ? 1.0 : t);
            return LowIsBad ? t : 1.0 - t;
        }
    }

    /// <summary>
    /// Draws a view into an RGBA buffer, top row first. The picture shows the face as seen from outside,
    /// looking at it, with sector zero at the top; sector numbers then increase counter-clockwise. The image is
    /// exact to one texel: cell edges are resolved per texel centre and must be sampled with point filtering.
    /// </summary>
    public static class ShieldMapRenderer
    {
        /// <summary>Face coordinates of a texel centre.</summary>
        public static void FaceFromTexel(int px, int py, int size, out double x, out double y)
        {
            double half = size * 0.5;
            x = (half - (py + 0.5)) / half;     // up on the screen is along sector zero
            y = (half - (px + 0.5)) / half;     // left on the screen is towards increasing sectors
        }

        /// <summary>The texel that holds a face point; false outside the image.</summary>
        public static bool TexelFromFace(double x, double y, int size, out int px, out int py)
        {
            double half = size * 0.5;
            px = (int)Math.Floor(half - y * half);
            py = (int)Math.Floor(half - x * half);
            return px >= 0 && py >= 0 && px < size && py < size;
        }

        /// <summary>Texture coordinates (0..1, v up) of a face point, for an overlay that reuses the image.</summary>
        public static void UvFromFace(double x, double y, out double u, out double v)
        {
            u = (1.0 - y) * 0.5;
            v = (1.0 + x) * 0.5;
        }

        /// <summary>Fills <paramref name="cells"/> with the cell of each texel centre, -1 outside the disc.</summary>
        public static void CellMap(ShieldLayout layout, int size, int[] cells)
        {
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    double x, y;
                    FaceFromTexel(px, py, size, out x, out y);
                    cells[py * size + px] = layout.CellAt(x, y);
                }
            }
        }

        /// <param name="cells">The cell map of this layout and size (<see cref="CellMap"/>).</param>
        /// <param name="grid">True to draw flat cells with borders instead of the smoothed field.</param>
        /// <param name="selected">Cell to outline, or -1.</param>
        /// <param name="rgba">4 bytes per texel, top row first.</param>
        public static void Render(ShieldMapData data, ShieldView view, MapScale scale, int[] cells, int size, bool grid, int selected, byte[] rgba)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (cells == null || cells.Length < size * size || rgba == null || rgba.Length < size * size * 4)
            {
                throw new ArgumentException("buffers are too small for the image");
            }
            double[] values = data.Values(view);
            CellFlags[] flags = data.Flags;
            CellFlags marks = ShieldMapData.Marks(view);
            ShieldLayout layout = data.Layout;

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    int p = py * size + px;
                    int o = p * 4;
                    int cell = cells[p];
                    if (cell < 0)
                    {
                        rgba[o] = rgba[o + 1] = rgba[o + 2] = rgba[o + 3] = 0;
                        continue;
                    }
                    CellFlags f = flags[cell] & marks;
                    double value;
                    if (grid || f != CellFlags.None)
                    {
                        value = values[cell];
                    }
                    else
                    {
                        double x, y;
                        FaceFromTexel(px, py, size, out x, out y);
                        value = ShieldField.Sample(layout, values, flags, marks, x, y);
                    }

                    double r, g, b;
                    if ((f & CellFlags.Exposed) != 0)
                    {
                        r = 0.22;
                        g = 0.23;
                        b = 0.26;
                    }
                    else if (double.IsNaN(value))
                    {
                        r = 0.35;
                        g = 0.37;
                        b = 0.40;
                    }
                    else
                    {
                        Colour(scale.Goodness(value), out r, out g, out b);
                    }

                    // Marks that do not depend on colour: one pattern per flag.
                    if ((f & CellFlags.Exposed) != 0 && ((px + py) & 7) < 2)
                    {
                        r = g = b = 0.05;
                    }
                    if ((f & CellFlags.CriticalIntegrity) != 0 && (px & 7) < 2 && (py & 7) < 2)
                    {
                        r = g = b = 1.0;
                    }
                    if ((f & CellFlags.BondAtRisk) != 0 && (px & 7) == 4)
                    {
                        r = g = b = 0.0;
                    }
                    if ((f & CellFlags.LowReserve) != 0 && (py & 7) == 4)
                    {
                        r = g = b = 0.0;
                    }

                    // Edges: every cell in grid mode; cells marked in this view and the selected cell always.
                    int right = px + 1 < size ? cells[p + 1] : -1;
                    int below = py + 1 < size ? cells[p + size] : -1;
                    int left = px > 0 ? cells[p - 1] : -1;
                    int above = py > 0 ? cells[p - size] : -1;
                    bool edge = right != cell || below != cell;
                    bool anyEdge = edge || left != cell || above != cell;
                    if (cell == selected && anyEdge)
                    {
                        r = 0.25;
                        g = 0.62;
                        b = 1.0;
                    }
                    else if (edge && (grid || f != CellFlags.None || (right >= 0 && (flags[right] & marks) != CellFlags.None) || (below >= 0 && (flags[below] & marks) != CellFlags.None)))
                    {
                        r = g = b = 0.0;
                    }

                    rgba[o] = Byte(r);
                    rgba[o + 1] = Byte(g);
                    rgba[o + 2] = Byte(b);
                    rgba[o + 3] = 255;
                }
            }
        }

        /// <summary>Green (1) through amber to red (0).</summary>
        public static void Colour(double goodness, out double r, out double g, out double b)
        {
            if (goodness > 0.5)
            {
                Lerp(0.98, 0.60, 0.12, 0.12, 0.70, 0.34, (goodness - 0.5) * 2.0, out r, out g, out b);
            }
            else if (goodness >= 0.2)
            {
                Lerp(1.00, 0.24, 0.08, 0.98, 0.60, 0.12, (goodness - 0.2) / 0.3, out r, out g, out b);
            }
            else
            {
                Lerp(0.60, 0.035, 0.035, 0.95, 0.16, 0.08, goodness / 0.2, out r, out g, out b);
            }
        }

        private static void Lerp(double r0, double g0, double b0, double r1, double g1, double b1, double t, out double r, out double g, out double b)
        {
            t = t < 0.0 ? 0.0 : (t > 1.0 ? 1.0 : t);
            r = r0 + (r1 - r0) * t;
            g = g0 + (g1 - g0) * t;
            b = b0 + (b1 - b0) * t;
        }

        private static byte Byte(double value)
        {
            return (byte)Math.Max(0.0, Math.Min(255.0, Math.Round(value * 255.0)));
        }
    }
}
