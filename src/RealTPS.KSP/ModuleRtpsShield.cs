using System;
using System.Collections.Generic;
using System.Globalization;
using RealTPS.Core.Materials;
using RealTPS.Core.Physics;
using KSP.Localization;
using UnityEngine;

namespace RealTPS
{
    /// <summary>
    /// Thermal protection of one enrolled host part: its material and the state of its cells.
    ///
    /// Ownership:
    /// - the stock thermal engine owns convection, reradiation, conduction and heat storage;
    /// - this module owns the material-loss energy sink, the zero-sink erosion channels, the per-cell
    ///   inventory and damage, the heat that exposed cells pass on, and the surface properties on the part
    ///   (emissivity, the native limit, the skin-to-interior conduction multiplier);
    /// - B9PartSwitch owns selection, editor mass/cost and the resource in the editor;
    /// - in flight the committed profile snapshot and the shield state are authoritative. A shield in flight
    ///   is never transmuted into another material, onto another grid or onto another construction.
    ///
    /// The physics step itself is driven by <see cref="ShieldCoordinator"/>, once per vessel, so that every
    /// thermal transaction of a step is planned before any is submitted.
    /// </summary>
    public class ModuleRtpsShield : PartModule
    {
        // ----- host description (part config; written by the enrolment patch) -----

        /// <summary>Protected area, m^2.</summary>
        [KSPField] public double tpsArea;

        /// <summary><c>moduleID</c> of the B9PartSwitch module that selects the material.</summary>
        [KSPField] public string switcherModuleId = "rtpsMaterial";

        /// <summary>
        /// The <b>requested</b> material: the part config value, replaced by the active B9 subtype's
        /// <c>MODULE/DATA</c>. It is a request, not the state: see <see cref="committedMaterialId"/>.
        /// </summary>
        [KSPField] public string materialId = string.Empty;

        // ----- persisted state -----

        /// <summary>Remaining consumable inventory, kg: the sum of the cells. The resource mirrors it.</summary>
        [KSPField(isPersistant = true)] public double tpsInventory;

        /// <summary>Consumable capacity at commit, kg.</summary>
        [KSPField(isPersistant = true)] public double tpsCapacity;

        // ----- read-only contract and display -----

        /// <summary>True when no consumable is left anywhere on the face.</summary>
        [KSPField] public bool tpsDepleted;

        [KSPField(guiName = "#LOC_RTPS_ui_sink", guiFormat = "F1", guiUnits = " kW")]
        public double sinkPowerKw;

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "#LOC_RTPS_ui_material")]
        public string uiMaterial = string.Empty;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "#LOC_RTPS_ui_status")]
        public string uiStatus = string.Empty;

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = "#LOC_RTPS_ui_remaining")]
        public string uiRemaining = string.Empty;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "#LOC_RTPS_ui_limit")]
        public string uiLimit = string.Empty;

        // ----- runtime only -----

        private enum Mode
        {
            Unresolved,
            Active,
        }

        [NonSerialized] private Mode mode = Mode.Unresolved;
        [NonSerialized] private MaterialDefinition material;
        [NonSerialized] private bool started;
        [NonSerialized] private PartResource mirror;
        [NonSerialized] private float nextUiRefresh;

        // ----- public read-only API -----

        /// <summary>True when a valid committed profile drives this shield.</summary>
        public bool IsActive
        {
            get { return mode == Mode.Active && material != null; }
        }

        /// <summary>Remaining share of the consumable over the whole face, 0..1; 1 for materials without one.</summary>
        public double RemainingFraction
        {
            get
            {
                if (mode != Mode.Active || material == null || !material.HasConsumable || !(tpsCapacity > 0.0))
                {
                    return 1.0;
                }
                return AblationSolver.Clamp01(tpsInventory / tpsCapacity);
            }
        }

        /// <summary>Equivalent remaining thickness of the consumable layer averaged over the face, m.</summary>
        public double RemainingThickness
        {
            get { return mode == Mode.Active && material != null && material.HasConsumable ? AblationSolver.EquivalentThickness(material, tpsInventory, tpsArea) : 0.0; }
        }

        // ================================================================================================
        // Persistence
        // ================================================================================================

        // ================================================================================================
        // Lifecycle
        // ================================================================================================

        public override void OnStart(StartState startState)
        {
            base.OnStart(startState);
            started = true;
        }

        public override void OnStartFinished(StartState startState)
        {
            base.OnStartFinished(startState);
            try
            {
                if (HighLogic.LoadedSceneIsEditor)
                {
                    StartEditor();
                }
                else if (HighLogic.LoadedSceneIsFlight)
                {
                    StartFlight();
                }
            }
            catch (Exception e)
            {
                SetInert("internal error: " + e.Message);
                RtpsLog.Error("start failed on " + PartLabel() + ": " + e);
            }
            RefreshUi();
        }

        // ----- editor -----

        private void StartEditor()
        {
            CommitRequested(false);
        }

        /// <summary>
        /// Commits the requested material: validates it against the catalogue and the host, stores its profile
        /// snapshot and records inventory and switcher mass. An invalid request is recorded as such, so that
        /// the part is inert in flight instead of silently protected. The cells themselves are laid out when
        /// the craft first enters flight, on the grid and the construction the part has at that moment.
        /// </summary>
        private void CommitRequested(bool quiet)
        {
            MaterialDefinition requested;
            string problem = null;
            if (!RtpsCatalogue.Current.TryGet(materialId, out requested))
            {
                problem = Localizer.Format("#LOC_RTPS_msg_unknown_material", materialId);
            }

            material = requested;
            mode = Mode.Active;
            CaptureInventoryFromPart();
        }

        private void CaptureInventoryFromPart()
        {
            PartResource resource = material != null && material.HasConsumable ? part.Resources.Get(material.ConsumableResource) : null;
            if (resource != null)
            {
                tpsCapacity = resource.maxAmount;
                tpsInventory = Math.Min(resource.amount, resource.maxAmount);
            }
            else
            {
                tpsCapacity = 0.0;
                tpsInventory = 0.0;
            }
        }

        // ----- flight -----

        private void StartFlight()
        {
        }

        // ================================================================================================
        // Physics step (driven by ShieldCoordinator, once per vessel and physics step)
        // ================================================================================================

        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight && vessel != null)
            {
                ShieldCoordinator.Step(vessel);
            }
        }

        /// <summary>Phase 1: consumption, damage, the sink and the requests of exposed cells. Nothing is submitted.</summary>
        internal void Plan(double dt)
        {

            double skinTemp = part.skinTemperature;

            var input = new ShieldStepInput
            {
                Dt = dt,
                SkinTemp = skinTemp,
                ExposedSkinHeatCapacity = ShieldCoordinator.ExposedSkinCapacity(part),
            };
            ShieldSolver.Step(material, state, input, result);

            tpsInventory = state.TotalInventory;
            if (mirror != null)
            {
                mirror.amount = tpsInventory;
            }
            sinkPowerKw = dt > 0.0 ? result.HeatAbsorbed * 0.001 / dt : 0.0;
        }

        /// <summary>Phase 5: surface properties for the engine's next step, flags, notifications.</summary>
        internal void Finish(double dt)
        {

            bool depleted = material.HasConsumable && tpsInventory <= AblationSolver.DepletionThreshold;
            if (depleted && !tpsDepleted)
            {
                tpsDepleted = true;
                    ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_RTPS_msg_depleted", part.partInfo.title), 8f, ScreenMessageStyle.UPPER_CENTER);
                RtpsLog.Info(PartLabel() + ": thermal protection exhausted at skin " + F(part.skinTemperature) + " K");
            }

        }

        // ================================================================================================
        // Display
        // ================================================================================================

        public void Update()
        {
            if (!started || Time.unscaledTime < nextUiRefresh)
            {
                return;
            }
            nextUiRefresh = Time.unscaledTime + 0.25f;
            RefreshUi();
        }

        private void RefreshUi()
        {
            bool flight = HighLogic.LoadedSceneIsFlight;
            bool active = mode == Mode.Active && material != null;

            uiMaterial = DisplayName();
            uiStatus = StatusText();

            bool consumable = active && tpsCapacity > 0.0;
            BaseField remaining = Fields["uiRemaining"];
            if (remaining != null)
            {
                remaining.guiActive = flight && consumable;
            }
            if (consumable)
            {
                uiRemaining = (100.0 * RemainingFraction).ToString("F0", CultureInfo.InvariantCulture) + "%  (" + (RemainingThickness * 1000.0).ToString("F1", CultureInfo.InvariantCulture) + " mm)";
            }

            BaseField limit = Fields["uiLimit"];
            if (limit != null)
            {
                limit.guiActive = active;
                limit.guiActiveEditor = active;
            }
            if (active)
            {
                double surface = material.MaxSurfaceTemp;
                uiLimit = surface.ToString("F0", CultureInfo.InvariantCulture) + " K";
            }

        }

        private string DisplayName()
        {
            MaterialDefinition listed;
            string name;
            if (RtpsCatalogue.Current.TryGet(materialId, out listed) && !string.IsNullOrEmpty(listed.DisplayName))
            {
                name = Localizer.Format(listed.DisplayName);
            }
            else if (material != null && !string.IsNullOrEmpty(material.DisplayName))
            {
                name = Localizer.Format(material.DisplayName);
            }
            else
            {
                name = materialId;
            }
            return name;
        }

        private string StatusText()
        {
            switch (mode)
            {
                case Mode.Unresolved:
                    return Localizer.Format("#LOC_RTPS_status_unresolved");
            }
            if (!HighLogic.LoadedSceneIsFlight)
            {
                return Localizer.Format("#LOC_RTPS_status_ready");
            }
            if (tpsDepleted)
            {
                return Localizer.Format("#LOC_RTPS_status_depleted");
            }
            if (sinkPowerKw > 0.0)
            {
                return Localizer.Format("#LOC_RTPS_status_ablating");
            }
            return Localizer.Format("#LOC_RTPS_status_nominal");
        }

        // ================================================================================================
        // Helpers
        // ================================================================================================

        internal string PartLabel()
        {
            string name = part != null && part.partInfo != null ? part.partInfo.name : "part";
            return name + "#" + (part != null ? part.persistentId.ToString(CultureInfo.InvariantCulture) : "?");
        }

        private static string F(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
