using System;
using System.Collections.Generic;
using System.Globalization;
using RealTPS.Core.Shield;
using KSP.Localization;
using KSP.UI.Screens;
using UnityEngine;

namespace RealTPS
{
    /// <summary>
    /// The shield window (docs/wiki/Shield-window.md): summary cards, a face-on map with view
    /// tabs on the left, flight numbers, the selected cell and the alerts on the right.
    /// The map shows the protected face as seen from outside, sector zero at the top, smoothed between cells while
    /// they are healthy; each view marks only the alerts about what it measures, drawn flat with a hard edge and
    /// a pattern; exact numbers and the worst cell are always listed. No markers around the face. The image is
    /// produced by the core (<see cref="ShieldMapRenderer"/>) and uploaded with point filtering.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class ShieldDiagnostics : MonoBehaviour
    {
        private const string LockName = "RealTPS.Diagnostics";
        private const int MapSize = 256;
        private const int Margin = 22;
        private const float RefreshInterval = 0.4f;
        private const float Width = 760f;
        private const float Height = 716f;
        private const float EmptyHeight = 132f;

        private static ShieldDiagnostics instance;

        private readonly List<ModuleRtpsShield> shields = new List<ModuleRtpsShield>();
        private readonly ShieldMapData data = new ShieldMapData(ShieldLayout.MaxCells);
        private readonly int[] cells = new int[MapSize * MapSize];
        private readonly byte[] rgba = new byte[MapSize * MapSize * 4];
        private readonly Color32[] pixels = new Color32[MapSize * MapSize];
        private readonly PartHighlight highlight = new PartHighlight();
        private readonly ShieldWindowStyle style = new ShieldWindowStyle();

        private ApplicationLauncherButton button;
        private Texture2D icon;
        private Texture2D map;
        private Texture2D legend;
        private ShieldView legendView = (ShieldView)(-1);
        private double legendBond = -1.0;
        private ShieldLayout cellLayout;
        private ModuleRtpsShield selected;
        private Vessel cachedVessel;
        private ShieldView view = ShieldView.Reserve;
        private bool grid;
        private int selectedCell = -1;
        private int hoverCell = -1;
        private bool open;
        private bool uiVisible = true;
        private bool locked;
        private bool faulted;
        private bool dirty = true;
        private bool toolbarHover;
        private float nextScan;
        private float nextRead;
        private Rect bounds = new Rect(60, 80, Width, Height);
        private Rect mapRect;

        // Read at each refresh, so that drawing reads no part state.
        private bool hasData;
        private string partTitle = string.Empty;
        private string materialName = string.Empty;
        private double skinK, skinLimitK, internalK, internalLimitK, pressureKPa, speed, tiltDeg, sinkKw, breachKw;
        private double reserve, inventoryKg, capacityKg, meanIntegrity;

        // Rim markers, as face angles (radians from sector zero); NaN = hidden.

        public static void Show(ModuleRtpsShield shield)
        {
            if (instance == null)
            {
                return;
            }
            instance.open = true;
            instance.Select(shield);
            if (instance.button != null)
            {
                instance.button.SetTrue(false);
            }
        }

        public void Awake()
        {
            instance = this;
            GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(LauncherDestroyed);
            GameEvents.onHideUI.Add(HideUi);
            GameEvents.onShowUI.Add(ShowUi);
            if (ApplicationLauncher.Ready)
            {
                AddButton();
            }
        }

        public void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(AddButton);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(LauncherDestroyed);
            GameEvents.onHideUI.Remove(HideUi);
            GameEvents.onShowUI.Remove(ShowUi);
            ReleaseLock();
            highlight.Clear();
            if (button != null && ApplicationLauncher.Instance != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(button);
            }
            style.Destroy();
            DestroyTexture(icon);
            DestroyTexture(map);
            DestroyTexture(legend);
            if (instance == this)
            {
                instance = null;
            }
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture != null)
            {
                Destroy(texture);
            }
        }

        private void AddButton()
        {
            if (button != null || ApplicationLauncher.Instance == null)
            {
                return;
            }
            try
            {
                if (icon == null)
                {
                    icon = MakeIcon();
                }
                button = ApplicationLauncher.Instance.AddModApplication(OnOpen, OnClose,
                    delegate { toolbarHover = true; }, delegate { toolbarHover = false; }, null, null,
                    ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW, icon);
                if (open)
                {
                    button.SetTrue(false);
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void LauncherDestroyed()
        {
            button = null;
            toolbarHover = false;
        }

        private void OnOpen()
        {
            open = true;
            nextScan = nextRead = 0f;
        }

        private void OnClose()
        {
            open = false;
            ReleaseLock();
            highlight.Clear();
        }

        private void Close()
        {
            OnClose();
            if (button != null)
            {
                button.SetFalse(false);
            }
        }

        private void HideUi()
        {
            uiVisible = false;
            ReleaseLock();
            highlight.Clear();
        }

        private void ShowUi()
        {
            uiVisible = true;
        }

        private void Select(ModuleRtpsShield shield)
        {
            highlight.Clear();
            selected = shield;
            selectedCell = -1;
            hoverCell = -1;
            hasData = false;
            nextRead = 0f;
            dirty = true;
        }

        // ================================================================================================

        public void Update()
        {
            if (!open || !uiVisible || faulted)
            {
                ReleaseLock();
                highlight.Clear();
                return;
            }
            try
            {
                if (cachedVessel != FlightGlobals.ActiveVessel || Time.unscaledTime >= nextScan)
                {
                    Scan();
                }
                if (selected != null && Time.unscaledTime >= nextRead)
                {
                    nextRead = Time.unscaledTime + RefreshInterval;
                    Read();
                }
                if (selected != null && selected.part != null)
                {
                    highlight.Select(selected.part);
                    highlight.Apply();
                }
                else
                {
                    highlight.Clear();
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void Scan()
        {
            cachedVessel = FlightGlobals.ActiveVessel;
            nextScan = Time.unscaledTime + 1.0f;
            shields.Clear();
            if (cachedVessel != null && cachedVessel.loaded && cachedVessel.parts != null)
            {
                for (int p = 0; p < cachedVessel.parts.Count; p++)
                {
                    Part part = cachedVessel.parts[p];
                    if (part == null)
                    {
                        continue;
                    }
                    var shield = part.FindModuleImplementing<ModuleRtpsShield>();
                    if (shield != null && shield.State != null)
                    {
                        shields.Add(shield);
                    }
                }
            }
            if (selected == null || !shields.Contains(selected))
            {
                Select(shields.Count > 0 ? shields[0] : null);
            }
        }

        private void Read()
        {
            ShieldState state = selected.State;
            if (state == null || selected.CommittedMaterial == null || selected.part == null || selected.vessel == null)
            {
                hasData = false;
                return;
            }
            data.Fill(selected.CommittedMaterial, state, RtpsDifficulty.CurrentSettings);
            if (!state.Layout.Equals(cellLayout))
            {
                cellLayout = state.Layout;
                ShieldMapRenderer.CellMap(cellLayout, MapSize, cells);
            }
            if (selectedCell >= data.Count)
            {
                selectedCell = -1;
            }
            Part part = selected.part;
            Vessel vessel = selected.vessel;
            partTitle = part.partInfo != null ? part.partInfo.title : part.name;
            materialName = selected.MaterialDisplayName;
            skinK = part.skinTemperature;
            skinLimitK = part.skinMaxTemp;
            internalK = part.temperature;
            internalLimitK = part.maxTemp;
            pressureKPa = vessel.dynamicPressurekPa;
            speed = vessel.srfSpeed;
            Incidence flow = selected.Flow;
            tiltDeg = flow.Defined ? Math.Atan2(flow.Tilt, flow.Facing) * 180.0 / Math.PI : double.NaN;
            sinkKw = selected.sinkPowerKw;
            breachKw = selected.breachPowerKw;
            reserve = selected.RemainingFraction;
            inventoryKg = selected.tpsInventory;
            capacityKg = selected.tpsCapacity;
            meanIntegrity = data.HasDamageState ? state.MeanIntegrity : double.NaN;
            hasData = true;
            dirty = true;
        }

        private void Redraw()
        {
            if (!dirty || cellLayout == null || !hasData)
            {
                return;
            }
            dirty = false;
            if (map == null)
            {
                map = new Texture2D(MapSize, MapSize, TextureFormat.RGBA32, false);
                // Categories must not be blended back together: point filtering (DISPLAY_PROPOSAL D3).
                map.filterMode = FilterMode.Point;
                map.wrapMode = TextureWrapMode.Clamp;
            }
            ShieldMapRenderer.Render(data, view, Scale(), cells, MapSize, grid, selectedCell, rgba);
            // The buffer's first row is the top of the picture; a texture's first row is its bottom.
            for (int py = 0; py < MapSize; py++)
            {
                int row = (MapSize - 1 - py) * MapSize;
                int src = py * MapSize * 4;
                for (int px = 0; px < MapSize; px++)
                {
                    int o = src + px * 4;
                    pixels[row + px] = new Color32(rgba[o], rgba[o + 1], rgba[o + 2], rgba[o + 3]);
                }
            }
            map.SetPixels32(pixels);
            map.Apply(false, false);
        }

        private MapScale Scale()
        {
            ShieldState state = selected != null ? selected.State : null;
            double bond = state != null && selected.CommittedMaterial != null ? state.Construction.BondLimit(selected.CommittedMaterial) : 0.0;
            return MapScale.For(view, bond);
        }

        // ================================================================================================

        /// <summary>The whole window shrinks on screens too small for it, never grows.</summary>
        private static float UiScale
        {
            get { return Mathf.Min(1f, Mathf.Min(Screen.width / (Width + 30f), Screen.height / (Height + 30f))); }
        }

        private bool Showing
        {
            get { return hasData && selected != null && selected.State != null && cellLayout != null; }
        }

        public void OnGUI()
        {
            if (!uiVisible || faulted || (!open && !toolbarHover))
            {
                return;
            }
            GUISkin oldSkin = GUI.skin;
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColour = GUI.color;
            try
            {
                GUI.skin = HighLogic.Skin;
                if (!style.Built)
                {
                    style.Build();
                }
                float scale = UiScale;
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
                float width = Screen.width / scale;
                float height = Screen.height / scale;
                if (open)
                {
                    if (Event.current.type == EventType.Layout)
                    {
                        Redraw();
                    }
                    bounds.width = Width;
                    bounds.height = Showing ? Height : EmptyHeight;
                    bounds.x = Mathf.Clamp(bounds.x, 0f, Mathf.Max(0f, width - bounds.width));
                    bounds.y = Mathf.Clamp(bounds.y, 0f, Mathf.Max(0f, height - bounds.height));
                    bounds = GUI.Window(GetInstanceID(), bounds, DrawWindow, string.Empty, style.Window);
                    if (!open)
                    {
                        ReleaseLock();
                        return;
                    }
                    UpdateLock(scale);
                }
                else
                {
                    // Hovering the toolbar button names the window.
                    float x = Mathf.Clamp(Input.mousePosition.x / scale - 180f, 0f, Mathf.Max(0f, width - 230f));
                    float y = Mathf.Clamp((Screen.height - Input.mousePosition.y) / scale + 18f, 0f, Mathf.Max(0f, height - 30f));
                    GUI.Box(new Rect(x, y, 230f, 28f), string.Empty, style.PanelBox);
                    GUI.Label(new Rect(x + 8f, y + 5f, 214f, 22f), L("#LOC_RTPS_diag_title"), style.Small);
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
            finally
            {
                GUI.skin = oldSkin;
                GUI.matrix = oldMatrix;
                GUI.color = oldColour;
            }
        }

        private void DrawWindow(int id)
        {
            GUI.Label(new Rect(20f, 11f, 640f, 26f), L("#LOC_RTPS_diag_title"), style.Title);
            if (GUI.Button(new Rect(Width - 43f, 10f, 27f, 25f), "X", style.Action))
            {
                Close();
            }

            if (!Showing)
            {
                GUI.Box(new Rect(16f, 45f, Width - 32f, 70f), string.Empty, style.PanelBox);
                GUI.Label(new Rect(30f, 56f, Width - 60f, 50f), L("#LOC_RTPS_diag_no_shield"), style.Small);
                GUI.DragWindow(new Rect(0f, 0f, Width - 50f, 38f));
                return;
            }

            DrawHeader();
            DrawCards();
            DrawMapPanel();
            DrawFlight();
            DrawCell();
            DrawAlerts();
            DrawFooter();
            DrawHoverTip();
            GUI.DragWindow(new Rect(0f, 0f, Width - 50f, 38f));
        }

        private void DrawHeader()
        {
            GUI.Box(new Rect(16f, 45f, 728f, 39f), string.Empty, style.PanelBox);
            bool several = shields.Count > 1;
            bool wasEnabled = GUI.enabled;
            GUI.enabled = several;
            if (GUI.Button(new Rect(23f, 51f, 30f, 26f), "<", style.Action))
            {
                Step(-1);
            }
            if (GUI.Button(new Rect(707f, 51f, 30f, 26f), ">", style.Action))
            {
                Step(1);
            }
            GUI.enabled = wasEnabled;
            GUI.Label(new Rect(61f, 50f, 560f, 28f), partTitle + "  -  " + materialName, style.Centered);
            GUI.Label(new Rect(626f, 56f, 72f, 22f), (shields.IndexOf(selected) + 1).ToString(CultureInfo.InvariantCulture) + " / "
                + shields.Count.ToString(CultureInfo.InvariantCulture), style.LabelRight);
        }

        private void DrawCards()
        {
            const float y = 97f, w = 176f, h = 67f;
            bool damage = data.HasDamageState;
            Card(new Rect(16f, y, w, h), L("#LOC_RTPS_diag_card_integrity"),
                damage ? Pct(meanIntegrity) : "--", false, ShieldWindowStyle.Condition(damage ? meanIntegrity : double.NaN), null);

            if (data.Consumable)
            {
                Card(new Rect(200f, y, w, h), L("#LOC_RTPS_diag_card_reserve"), Pct(reserve), false, ShieldWindowStyle.Condition(reserve),
                    L("#LOC_RTPS_diag_card_kg", Num(inventoryKg, "F1"), Num(capacityKg, "F1")));
            }
            else
            {
                Card(new Rect(200f, y, w, h), L("#LOC_RTPS_diag_card_reserve"), L("#LOC_RTPS_diag_not_consumed"), true, ShieldWindowStyle.Text, null);
            }

            Color exposed = !damage ? ShieldWindowStyle.Muted : data.ExposedCells > 0 ? ShieldWindowStyle.Danger : ShieldWindowStyle.Good;
            Card(new Rect(384f, y, w, h), L("#LOC_RTPS_diag_card_exposed"), damage ? Pct(data.ExposedArea) : "--", false, exposed,
                damage ? L("#LOC_RTPS_diag_card_cells", data.ExposedCells.ToString(CultureInfo.InvariantCulture)) : null);

            if (data.Failure != ShieldFailure.None)
            {
                Card(new Rect(568f, y, w, h), L("#LOC_RTPS_diag_card_structure"), L("#LOC_RTPS_diag_failed"), true, ShieldWindowStyle.Danger,
                    L("#LOC_RTPS_diag_failure_lost_short"));
            }
            else
            {
                Card(new Rect(568f, y, w, h), L("#LOC_RTPS_diag_card_structure"), damage ? Pct(data.Structure) : "--", false,
                    ShieldWindowStyle.Condition(damage ? data.Structure : double.NaN), damage ? L("#LOC_RTPS_diag_failure_none_short") : null);
            }
        }

        private void Card(Rect rect, string label, string value, bool word, Color colour, string note)
        {
            GUI.Box(rect, string.Empty, style.PanelBox);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 9f, rect.width - 24f, 18f), label, style.Label);
            GUIStyle valueStyle = word ? style.Word : style.Big;
            ShieldWindowStyle.Coloured(new Rect(rect.x + 14f, rect.y + (word ? 33f : 29f), rect.width - 22f, 34f), value, valueStyle, colour);
            if (!string.IsNullOrEmpty(note))
            {
                GUI.Label(new Rect(rect.x + 60f, rect.y + 44f, rect.width - 70f, 18f), note, style.LabelRight);
            }
        }

        // ----- left: views and the map -----

        private void DrawMapPanel()
        {
            GUI.Box(new Rect(16f, 178f, 330f, 474f), string.Empty, style.PanelBox);
            bool wasEnabled = GUI.enabled;
            for (int v = 0; v < ShieldMapData.ViewCount; v++)
            {
                var candidate = (ShieldView)v;
                var tab = new Rect(25f + (v % 3) * 106f, v < 3 ? 187f : 217f, 100f, 26f);
                GUI.enabled = wasEnabled && Available(candidate);
                if (GUI.Button(tab, ViewName(candidate), view == candidate ? style.ActiveTab : style.Action) && view != candidate)
                {
                    view = candidate;
                    dirty = true;
                }
            }
            GUI.enabled = wasEnabled;
            if (GUI.Button(new Rect(237f, 217f, 100f, 26f), L("#LOC_RTPS_diag_grid"), grid ? style.ActiveTab : style.Action))
            {
                grid = !grid;
                dirty = true;
            }
            if (!Available(view))
            {
                view = ShieldView.Reserve;
                dirty = true;
            }

            mapRect = new Rect(31f + Margin, 250f + Margin, MapSize, MapSize);
            if (map != null)
            {
                GUI.DrawTexture(mapRect, map, ScaleMode.StretchToFill, true);
            }
            DrawCellLabels();
            HandleMouse();

            DrawLegend(new Rect(31f, 556f, 300f, 18f));
            GUI.Label(new Rect(29f, 578f, 306f, 52f), ViewAbout(view), style.Label);
        }

        /// <summary>What the view in use shows, under its legend.</summary>
        private static string ViewAbout(ShieldView v)
        {
            switch (v)
            {
                case ShieldView.Reserve: return L("#LOC_RTPS_diag_about_reserve");
                case ShieldView.Integrity: return L("#LOC_RTPS_diag_about_integrity");
                case ShieldView.SubstrateTemp: return L("#LOC_RTPS_diag_about_bond_temp");
                case ShieldView.BondMargin: return L("#LOC_RTPS_diag_about_margin");
                default: return L("#LOC_RTPS_diag_about_received");
            }
        }

        /// <summary>The marks of the view in use (each view marks only what it measures).</summary>
        private static string ViewMarks(ShieldView v)
        {
            switch (v)
            {
                case ShieldView.Reserve: return L("#LOC_RTPS_diag_marks_reserve");
                case ShieldView.Integrity: return L("#LOC_RTPS_diag_marks_integrity");
                case ShieldView.SubstrateTemp:
                case ShieldView.BondMargin: return L("#LOC_RTPS_diag_marks_bond");
                default: return L("#LOC_RTPS_diag_marks_none");
            }
        }

        private bool Available(ShieldView candidate)
        {
            switch (candidate)
            {
                case ShieldView.Reserve:
                    return true;
                case ShieldView.Integrity:
                case ShieldView.SubstrateTemp:
                case ShieldView.Received:
                    return data.HasDamageState;
                default:
                    return data.HasBondLimit;
            }
        }

        /// <summary>Screen position of a face point (polar, in face radii): sector zero up, sectors counter-clockwise.</summary>
        private Vector2 MapPoint(double azimuth, double radius)
        {
            Vector2 centre = mapRect.center;
            float half = MapSize * 0.5f;
            return new Vector2(centre.x - (float)(Math.Sin(azimuth) * radius) * half, centre.y - (float)(Math.Cos(azimuth) * radius) * half);
        }

        /// <summary>Cell numbers: every cell in grid view (up to 40 cells), otherwise only the selected and the worst.</summary>
        private void DrawCellLabels()
        {
            int worst = data.WorstCell(view);
            bool all = grid && data.Count <= 40;
            for (int i = 0; i < data.Count; i++)
            {
                if (!all && i != selectedCell && i != worst)
                {
                    continue;
                }
                Vector2 at = MapPoint(cellLayout.Angle(i), cellLayout.SampleRadius(i));
                Color colour = i == selectedCell ? ShieldWindowStyle.Blue : i == worst ? ShieldWindowStyle.Caution : Color.white;
                ShieldWindowStyle.Shadowed(new Rect(at.x - 16f, at.y - 9f, 32f, 18f), cellLayout.Label(i), style.Marker, colour);
            }
        }

        private void HandleMouse()
        {
            Event e = Event.current;
            hoverCell = -1;
            if (!mapRect.Contains(e.mousePosition) || cellLayout == null)
            {
                return;
            }
            double half = MapSize * 0.5;
            double x = (half - (e.mousePosition.y - mapRect.y)) / half;
            double y = (half - (e.mousePosition.x - mapRect.x)) / half;
            hoverCell = cellLayout.CellAt(x, y);
            if (e.type == EventType.MouseDown && e.button == 0 && hoverCell >= 0)
            {
                selectedCell = hoverCell == selectedCell ? -1 : hoverCell;
                dirty = true;
                e.Use();
            }
        }

        private void DrawLegend(Rect row)
        {
            MapScale scale = Scale();
            double bond = scale.Max;
            if (legend == null || legendView != view || legendBond != bond)
            {
                if (legend == null)
                {
                    legend = new Texture2D(128, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                }
                var line = new Color32[128];
                for (int i = 0; i < 128; i++)
                {
                    double value = scale.Min + (scale.Max - scale.Min) * i / 127.0;
                    double r, g, b;
                    ShieldMapRenderer.Colour(scale.Goodness(value), out r, out g, out b);
                    line[i] = new Color32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), 255);
                }
                legend.SetPixels32(line);
                legend.Apply(false, false);
                legendView = view;
                legendBond = bond;
            }
            GUI.Label(new Rect(row.x, row.y, 76f, row.height), Value(view, scale.Min), style.LabelRight);
            GUI.DrawTexture(new Rect(row.x + 82f, row.y + 4f, 136f, 10f), legend, ScaleMode.StretchToFill);
            GUI.Label(new Rect(row.x + 224f, row.y, 76f, row.height), Value(view, scale.Max), style.Label);
        }

        // ----- right: flight, cell, alerts -----

        private void DrawFlight()
        {
            GUI.Box(new Rect(354f, 178f, 390f, 180f), string.Empty, style.PanelBox);
            Metric(190f, L("#LOC_RTPS_diag_skin"), Num(skinK, "F0") + " / " + Num(skinLimitK, "F0") + " K");
            ShieldWindowStyle.Bar(new Rect(370f, 215f, 358f, 5f), skinLimitK > 0.0 ? skinK / skinLimitK : double.NaN, ShieldWindowStyle.Accent);
            Metric(226f, L("#LOC_RTPS_diag_internal"), Num(internalK, "F0") + " / " + Num(internalLimitK, "F0") + " K");
            Metric(251f, L("#LOC_RTPS_diag_pressure"), Num(pressureKPa, "F2") + " kPa");
            Metric(276f, L("#LOC_RTPS_diag_speed"), Num(speed, "F0") + " m/s");
            Metric(301f, L("#LOC_RTPS_diag_tilt"), double.IsNaN(tiltDeg) ? "--" : Num(tiltDeg, "F1") + "°");
            Metric(326f, L("#LOC_RTPS_diag_heat_flows"), Num(sinkKw, "F1") + " / " + Num(breachKw, "F1") + " kW");
        }

        private void Metric(float y, string label, string value)
        {
            GUI.Label(new Rect(370f, y, 190f, 24f), label, style.TextStyle);
            GUI.Label(new Rect(562f, y, 170f, 24f), value, style.TextStyle);
        }

        private void DrawCell()
        {
            GUI.Box(new Rect(354f, 366f, 390f, 168f), string.Empty, style.PanelBox);
            int cell = selectedCell;
            if (cell < 0 || cell >= data.Count)
            {
                GUI.Label(new Rect(370f, 376f, 358f, 26f), L("#LOC_RTPS_diag_cell_none"), style.Title);
                GUI.Label(new Rect(370f, 408f, 358f, 60f), L("#LOC_RTPS_diag_pick"), style.Small);
                return;
            }
            CellFlags flags = data.Flags[cell];
            GUI.Label(new Rect(370f, 376f, 358f, 26f), L("#LOC_RTPS_diag_cell", cellLayout.Label(cell)), style.Title);
            ShieldWindowStyle.Coloured(new Rect(370f, 402f, 358f, 18f), flags == CellFlags.None ? L("#LOC_RTPS_diag_cell_sound") : FlagText(flags), style.Label,
                flags == CellFlags.None ? ShieldWindowStyle.Good : (flags & (CellFlags.Exposed | CellFlags.CriticalIntegrity)) != 0 ? ShieldWindowStyle.Danger : ShieldWindowStyle.Caution);

            double reserveValue = data.Values(ShieldView.Reserve)[cell];
            double integrity = data.Values(ShieldView.Integrity)[cell];
            CellRow(422f, ShieldView.Reserve, reserveValue);
            ShieldWindowStyle.Bar(new Rect(370f, 445f, 358f, 5f), reserveValue, ShieldWindowStyle.Blue);
            CellRow(454f, ShieldView.Integrity, integrity);
            ShieldWindowStyle.Bar(new Rect(370f, 477f, 358f, 5f), integrity, ShieldWindowStyle.Condition(integrity));
            double bondTemp = data.Values(ShieldView.SubstrateTemp)[cell];
            double margin = data.Values(ShieldView.BondMargin)[cell];
            string bond = Value(ShieldView.SubstrateTemp, bondTemp);
            if (!double.IsNaN(margin))
            {
                bond += "  " + L("#LOC_RTPS_diag_margin_short", Value(ShieldView.BondMargin, margin));
            }
            GUI.Label(new Rect(370f, 486f, 190f, 22f), ViewName(ShieldView.SubstrateTemp), style.TextStyle);
            GUI.Label(new Rect(562f, 486f, 170f, 22f), bond, style.TextStyle);
            CellRow(508f, ShieldView.Received, data.Values(ShieldView.Received)[cell]);
        }

        private void CellRow(float y, ShieldView item, double value)
        {
            GUI.Label(new Rect(370f, y, 190f, 22f), ViewName(item), style.TextStyle);
            GUI.Label(new Rect(562f, y, 170f, 22f), Value(item, value), style.TextStyle);
        }

        private void DrawAlerts()
        {
            GUI.Box(new Rect(354f, 542f, 390f, 110f), string.Empty, style.PanelBox);
            GUI.Label(new Rect(370f, 548f, 358f, 18f), L("#LOC_RTPS_diag_alerts"), style.Label);
            if (!data.HasDamageState)
            {
                GUI.Label(new Rect(370f, 568f, 358f, 70f), L("#LOC_RTPS_diag_legacy"), style.Small);
                return;
            }
            Chip(0, L("#LOC_RTPS_diag_chip_exposed"), data.ExposedCells, true, ShieldWindowStyle.Danger);
            Chip(1, L("#LOC_RTPS_diag_chip_critical"), data.CriticalCells, true, ShieldWindowStyle.Danger);
            Chip(2, L("#LOC_RTPS_diag_chip_low"), data.LowReserveCells, data.Consumable, ShieldWindowStyle.Warning);
            Chip(3, L("#LOC_RTPS_diag_chip_bond"), data.BondRiskCells, data.HasBondLimit, ShieldWindowStyle.Caution);
            if (data.HasBondLimit)
            {
                GUI.Label(new Rect(370f, 612f, 358f, 18f), L("#LOC_RTPS_diag_margin",
                    double.IsNaN(data.SmallestBondMargin) ? "--" : data.SmallestBondMargin.ToString("F0", CultureInfo.InvariantCulture)), style.Label);
            }
            int worst = data.WorstCell(view);
            if (worst >= 0)
            {
                GUI.Label(new Rect(370f, 630f, 358f, 18f), L("#LOC_RTPS_diag_worst", ViewName(view), cellLayout.Label(worst), Value(view, data.Values(view)[worst])), style.Label);
            }
        }

        private void Chip(int index, string label, int count, bool applies, Color colour)
        {
            var rect = new Rect(366f + index * 93f, 568f, 87f, 40f);
            GUI.Box(rect, string.Empty, style.Chip);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 3f, rect.width - 12f, 16f), label, style.ChipLabel);
            ShieldWindowStyle.Coloured(new Rect(rect.x + 8f, rect.y + 16f, rect.width - 12f, 24f),
                applies ? count.ToString(CultureInfo.InvariantCulture) : "--", style.ChipCount, applies && count > 0 ? colour : ShieldWindowStyle.Muted);
        }

        private void DrawFooter()
        {
            Color colour = !data.HasDamageState ? ShieldWindowStyle.Muted : selected.IsStepping ? ShieldWindowStyle.Good : ShieldWindowStyle.Caution;
            ShieldWindowStyle.Coloured(new Rect(20f, 660f, 400f, 20f), StatusLine(), style.Label, colour);
            GUI.Label(new Rect(424f, 660f, 316f, 20f), L("#LOC_RTPS_diag_difficulty", RtpsDifficulty.DisplayName(RtpsDifficulty.Current.Preset)), style.LabelRight);
            GUI.Label(new Rect(20f, 680f, 724f, 32f), ViewMarks(view), style.Label);
        }

        /// <summary>The cell under the cursor, next to it, drawn last so that it lies over the panels.</summary>
        private void DrawHoverTip()
        {
            int cell = hoverCell;
            if (cell < 0 || cell >= data.Count)
            {
                return;
            }
            var lines = new List<string> { L("#LOC_RTPS_diag_cell", cellLayout.Label(cell)) };
            for (int v = 0; v < ShieldMapData.ViewCount; v++)
            {
                var item = (ShieldView)v;
                double value = data.Values(item)[cell];
                if (!double.IsNaN(value))
                {
                    lines.Add(ViewName(item) + ": " + Value(item, value));
                }
            }
            CellFlags flags = data.Flags[cell];
            if (flags != CellFlags.None)
            {
                lines.Add(FlagText(flags));
            }
            Vector2 mouse = Event.current.mousePosition;
            float h = 16f + 17f * lines.Count;
            var tip = new Rect(Mathf.Min(mouse.x + 16f, Width - 16f - 250f), Mathf.Clamp(mouse.y - h * 0.5f, 50f, Height - 8f - h), 250f, h);
            GUI.Box(tip, string.Empty, style.Chip);
            GUI.Label(new Rect(tip.x + 10f, tip.y + 7f, tip.width - 20f, h - 10f), string.Join("\n", lines.ToArray()), style.Small);
        }

        private static string FlagText(CellFlags flags)
        {
            var parts = new List<string>();
            if ((flags & CellFlags.Exposed) != 0)
            {
                parts.Add(L("#LOC_RTPS_diag_flag_exposed"));
            }
            if ((flags & CellFlags.CriticalIntegrity) != 0)
            {
                parts.Add(L("#LOC_RTPS_diag_flag_critical"));
            }
            if ((flags & CellFlags.LowReserve) != 0)
            {
                parts.Add(L("#LOC_RTPS_diag_flag_low"));
            }
            if ((flags & CellFlags.BondAtRisk) != 0)
            {
                parts.Add(L("#LOC_RTPS_diag_flag_bond"));
            }
            return string.Join(", ", parts.ToArray());
        }

        private string StatusLine()
        {
            if (!data.HasDamageState)
            {
                return L("#LOC_RTPS_diag_status_unavailable");
            }
            return L(selected.IsStepping ? "#LOC_RTPS_diag_status_live" : "#LOC_RTPS_diag_status_frozen");
        }

        private static string ViewName(ShieldView v)
        {
            switch (v)
            {
                case ShieldView.Reserve: return L("#LOC_RTPS_diag_view_reserve");
                case ShieldView.Integrity: return L("#LOC_RTPS_diag_view_integrity");
                case ShieldView.SubstrateTemp: return L("#LOC_RTPS_diag_view_bond_temp");
                case ShieldView.BondMargin: return L("#LOC_RTPS_diag_view_margin");
                default: return L("#LOC_RTPS_diag_view_received");
            }
        }

        private static string Value(ShieldView v, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "--";
            }
            switch (v)
            {
                case ShieldView.Reserve:
                case ShieldView.Integrity:
                    return Pct(value);
                case ShieldView.SubstrateTemp:
                case ShieldView.BondMargin:
                    return value.ToString("F0", CultureInfo.InvariantCulture) + " K";
                default:
                    return (value / 1.0e6).ToString("F1", CultureInfo.InvariantCulture) + " MJ/m²";
            }
        }

        private static string Pct(double fraction)
        {
            return double.IsNaN(fraction) || double.IsInfinity(fraction) ? "--" : (100.0 * fraction).ToString("F0", CultureInfo.InvariantCulture) + "%";
        }

        private static string Num(double value, string format)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? "--" : value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string L(string key, params string[] args)
        {
            return Localizer.Format(key, args);
        }

        private void Step(int direction)
        {
            if (shields.Count == 0)
            {
                return;
            }
            int index = Math.Max(0, shields.IndexOf(selected));
            Select(shields[(index + direction + shields.Count) % shields.Count]);
        }

        private void UpdateLock(float scale)
        {
            Vector2 mouse = new Vector2(Input.mousePosition.x / scale, (Screen.height - Input.mousePosition.y) / scale);
            bool hovering = bounds.Contains(mouse);
            if (hovering && !locked)
            {
                // Camera and part clicks only: the craft stays controllable while the window is under the mouse.
                InputLockManager.SetControlLock(ControlTypes.CAMERACONTROLS | ControlTypes.TWEAKABLES, LockName);
                locked = true;
            }
            else if (!hovering && locked && GUIUtility.hotControl == 0)
            {
                ReleaseLock();
            }
        }

        private void ReleaseLock()
        {
            if (locked)
            {
                InputLockManager.RemoveControlLock(LockName);
                locked = false;
            }
        }

        private void Fault(Exception e)
        {
            if (faulted)
            {
                return;
            }
            faulted = true;
            open = false;
            ReleaseLock();
            highlight.Clear();
            RtpsLog.Error("shield window stopped after an error: " + e);
        }

        /// <summary>The toolbar icon: a shield face with one hot cell.</summary>
        private static Texture2D MakeIcon()
        {
            var texture = new Texture2D(38, 38, TextureFormat.RGBA32, false);
            var colours = new Color32[38 * 38];
            var layout = new ShieldLayout(8, 1, 6.0);
            for (int y = 0; y < 38; y++)
            {
                for (int x = 0; x < 38; x++)
                {
                    double fx = (x - 18.5) / 16.0, fy = (y - 18.5) / 16.0;
                    int cell = layout.CellAt(fx, fy);
                    colours[y * 38 + x] = cell < 0 ? new Color32(0, 0, 0, 0)
                        : (layout.Sector(cell) == 1 && layout.Band(cell) == 1 ? new Color32(255, 155, 45, 255) : new Color32(52, 205, 112, 255));
                }
            }
            texture.SetPixels32(colours);
            texture.Apply(false, true);
            return texture;
        }
    }

    /// <summary>Temporarily owns only the selected part's highlight, never its materials.</summary>
    internal sealed class PartHighlight
    {
        private static readonly Color Blue = new Color(0.12f, 0.58f, 1f, 1f);
        private Part target;
        private Color oldColour;
        private Part.HighlightType oldType;
        private bool oldActive;
        private bool oldRecursive;

        public void Select(Part part)
        {
            if (target == part)
            {
                return;
            }
            Clear();
            if (part == null)
            {
                return;
            }
            target = part;
            oldColour = part.highlightColor;
            oldType = part.highlightType;
            oldActive = part.HighlightActive;
            oldRecursive = part.RecurseHighlight;
        }

        public void Apply()
        {
            if (target == null)
            {
                return;
            }
            target.SetHighlightType(Part.HighlightType.AlwaysOn);
            target.SetHighlightColor(Blue);
            target.SetHighlight(true, false);
        }

        public void Clear()
        {
            if (target != null && target.highlightType == Part.HighlightType.AlwaysOn)
            {
                target.SetHighlightType(oldType);
                target.SetHighlightColor(oldColour);
                target.SetHighlight(oldActive, oldRecursive);
            }
            target = null;
        }
    }
}
