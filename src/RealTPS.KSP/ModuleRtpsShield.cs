using System;
using System.Collections.Generic;
using System.Globalization;
using RealTPS.Core.Cfg;
using RealTPS.Core.Hosts;
using RealTPS.Core.Materials;
using RealTPS.Core.Physics;
using RealTPS.Core.Shield;
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
    public class ModuleRtpsShield : PartModule, IPartMassModifier
    {
        /// <summary>Version of the read-only state contract exposed to other mods.</summary>
        public const int ContractVersion = 2;

        /// <summary>Persisted state layout version.</summary>
        public const int StateVersion = 1;

        /// <summary>Marker id of a pre-RealTPS host in flight that had no stock ablator; RealTPS leaves it untouched.</summary>
        public const string LegacyUntreatedId = "legacy-untreated";

        /// <summary>Name of the event sent to the part when the shield state changes.</summary>
        public const string StateEventName = "OnRtpsStateChanged";

        private const string SnapshotNodeName = "RTPS_SNAPSHOT";
        private const string StockAblatorResource = "Ablator";
        private const int DownFace = 3; // DragCube.DragFace.YN, the face the stock ablator rule tests

        // ----- host description (part config; written by the enrolment patch) -----

        [KSPField] public string hostClass = string.Empty;
        [KSPField] public string hostCapabilities = string.Empty;

        /// <summary>Protected area, m^2.</summary>
        [KSPField] public double tpsArea;

        [KSPField] public double thicknessFactor = 1.0;
        [KSPField] public double includedDryArealDensity;

        /// <summary>Surface limit of the bare carrier once the shield has failed, K.</summary>
        [KSPField] public double substrateMaxTemp = 1100.0;

        /// <summary>True when RealTPS applies the stock occlusion rule to <c>part.heatConductivity</c>.</summary>
        [KSPField] public bool ownsConductivity = true;

        [KSPField] public double reentryConductivity = 0.01;

        /// <summary>Stock ablator capacity of the original part, kg; 0 when it had none.</summary>
        [KSPField] public double legacyAblatorCapacity;

        [KSPField] public string charModuleName = string.Empty;
        [KSPField] public float charMax = 0.85f;
        [KSPField] public float charMin = 0.1f;

        /// <summary><c>moduleID</c> of the B9PartSwitch module that selects the material.</summary>
        [KSPField] public string switcherModuleId = "rtpsMaterial";

        // Grid and face. A shield keeps the grid and the construction it was launched with.
        [KSPField] public int sectors = ShieldLayout.DefaultSectors;
        [KSPField] public int rings = ShieldLayout.DefaultRings;
        [KSPField] public double centerPercent = ShieldLayout.DefaultCenterPercent;
        [KSPField] public string protectedFace = "0, -1, 0";
        [KSPField] public string sectorZero = "1, 0, 0";

        /// <summary>
        /// <c>dedicated</c> (breach heat goes to the part behind) or <c>multifunction</c> (to the part's own
        /// interior). It no longer decides anything about failure: a failed shield always stays.
        /// </summary>
        [KSPField] public string partRole = ShieldFailurePolicy.RoleDedicated;

        [KSPField] public double backConductance = ShieldConstruction.DefaultBackConductance;
        [KSPField] public double substrateHeatCapacity = ShieldConstruction.DefaultSubstrateHeatCapacity;
        [KSPField] public double structureLimitTemp;
        [KSPField] public string deployAnimation = string.Empty;
        [KSPField] public double deployedRadius;

        /// <summary>
        /// The <b>requested</b> material: the part config value, replaced by the active B9 subtype's
        /// <c>MODULE/DATA</c>. It is a request, not the state: see <see cref="committedMaterialId"/>.
        /// </summary>
        [KSPField] public string materialId = string.Empty;

        // ----- persisted state -----

        /// <summary>0 = this part has never been committed by RealTPS.</summary>
        [KSPField(isPersistant = true)] public int rtpsStateVersion;

        /// <summary>The material this shield is built from. In flight it never changes.</summary>
        [KSPField(isPersistant = true)] public string committedMaterialId = string.Empty;

        /// <summary>Remaining consumable inventory, kg: the sum of the cells. The resource mirrors it.</summary>
        [KSPField(isPersistant = true)] public double tpsInventory;

        /// <summary>Consumable capacity at commit, kg.</summary>
        [KSPField(isPersistant = true)] public double tpsCapacity;

        /// <summary>Inventory pyrolysed so far, kg (drives the char emissivity; erosion is not counted).</summary>
        [KSPField(isPersistant = true)] public double tpsPyrolysed;

        /// <summary>Dry mass contributed by the B9 switcher when the material was committed, t.</summary>
        [KSPField(isPersistant = true)] public double committedSwitcherMass;

        /// <summary>True for a pre-RealTPS vessel that keeps its stock <c>Ablator</c> at the stock rate.</summary>
        [KSPField(isPersistant = true)] public bool legacyMode;

        /// <summary>Set once when the surface exceeded the material's reuse limit.</summary>
        [KSPField(isPersistant = true)] public bool reuseLimitExceeded;

        /// <summary>Highest exposed-skin temperature seen while active, K.</summary>
        [KSPField(isPersistant = true)] public double peakSurfaceTemp;

        [SerializeField] private string snapshotText = string.Empty;

        /// <summary>The stored <c>RTPS_SHIELD_STATE</c> node as text: what was loaded, until flight resolves it.</summary>
        [SerializeField] private string shieldStateText = string.Empty;

        // ----- read-only contract and display -----

        [KSPField] public int rtpsContractVersion = ContractVersion;

        /// <summary><c>unresolved</c>, <c>active</c>, <c>inert</c> or <c>legacy_untreated</c>.</summary>
        [KSPField] public string tpsMode = "unresolved";

        /// <summary>True when no consumable is left anywhere on the face.</summary>
        [KSPField] public bool tpsDepleted;

        [KSPField] public bool tpsShearOverload;

        /// <summary>True when the committed material is no longer offered by the part's selector.</summary>
        [KSPField] public bool tpsDetached;

        /// <summary>Share of the protected face that is exposed, 0..1.</summary>
        [KSPField] public double tpsExposedArea;

        /// <summary>Area-weighted mean integrity of the cells, 0..1.</summary>
        [KSPField] public double tpsIntegrity = 1.0;

        /// <summary>Structural integrity of the host behind its protection, 0..1.</summary>
        [KSPField] public double tpsStructure = 1.0;

        /// <summary><c>none</c> or <c>protection_lost</c>.</summary>
        [KSPField] public string tpsFailure = "none";

        [KSPField(guiName = "#LOC_RTPS_ui_sink", guiFormat = "F1", guiUnits = " kW")]
        public double sinkPowerKw;

        [KSPField(guiName = "#LOC_RTPS_ui_breach", guiFormat = "F1", guiUnits = " kW")]
        public double breachPowerKw;

        /// <summary>
        /// What exposed cells asked to pass on in the last step, kW, before the rays looked for a part behind
        /// them. Telemetry only: <see cref="breachPowerKw"/> is what was actually moved.
        /// </summary>
        [NonSerialized] private double breachRequestKw;

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "#LOC_RTPS_ui_material")]
        public string uiMaterial = string.Empty;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "#LOC_RTPS_ui_status")]
        public string uiStatus = string.Empty;

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = "#LOC_RTPS_ui_remaining")]
        public string uiRemaining = string.Empty;

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = "#LOC_RTPS_ui_integrity")]
        public string uiIntegrity = string.Empty;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "#LOC_RTPS_ui_limit")]
        public string uiLimit = string.Empty;

        // ----- runtime only -----

        private enum Mode
        {
            Unresolved,
            Active,
            Inert,
            LegacyUntreated
        }

        [NonSerialized] private Mode mode = Mode.Unresolved;
        [NonSerialized] private MaterialDefinition material;
        [NonSerialized] private ShieldState state;
        [NonSerialized] private ShieldStepResult result;
        [NonSerialized] private ShieldRouting routing;
        [NonSerialized] private FaceFrame frame;
        [NonSerialized] private Incidence flow;
        [NonSerialized] private string inertReason = string.Empty;
        [NonSerialized] private bool started;
        [NonSerialized] private bool reconciling;
        [NonSerialized] private bool resourceMissing;
        [NonSerialized] private bool stateRefused;
        [NonSerialized] private bool breachAnnounced;
        [NonSerialized] private bool stepping;
        [NonSerialized] private double massDelta;
        [NonSerialized] private PartResource mirror;
        [NonSerialized] private IScalarModule charModule;
        [NonSerialized] private ModuleAnimateGeneric deployModule;
        [NonSerialized] private ShieldPlumes plumes;
        [NonSerialized] private double plumePowerKw;

        /// <summary>What the coordinator submitted to this part's exposed skin in the last tick, kW (negative for a sink).</summary>
        [NonSerialized] internal double CommittedExposedKw;
        [NonSerialized] private float lastCharScalar = -1f;
        [NonSerialized] private bool baseCaptured;
        [NonSerialized] private double baseEmissive;
        [NonSerialized] private double baseSkinMaxTemp;
        [NonSerialized] private double baseSkinInternalMult;
        [NonSerialized] private double baseConductivity;
        [NonSerialized] private float nextUiRefresh;
        [NonSerialized] private double nextTelemetry;
        [NonSerialized] private bool oxidising;
        [NonSerialized] private double grantedBreach;
        [NonSerialized] private Part[] recipients;

        /// <summary><c>Time.fixedTime</c> of the last physics tick in which the coordinator handled this shield.</summary>
        [NonSerialized] internal double LastStepTime = double.NaN;
        [NonSerialized] private double[] recipientJoules;

        // ----- public read-only API -----

        /// <summary>True when a valid committed profile drives this shield.</summary>
        public bool IsActive
        {
            get { return mode == Mode.Active && material != null && state != null; }
        }

        /// <summary>The committed profile, or null while unresolved, inert or untreated.</summary>
        public MaterialDefinition CommittedMaterial
        {
            get { return mode == Mode.Active ? material : null; }
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

        /// <summary>Why the shield is inert; empty otherwise.</summary>
        public string InertReason
        {
            get { return inertReason; }
        }

        // ----- access for the coordinator and the diagnostics window (same assembly) -----

        internal ShieldState State
        {
            get { return IsActive ? state : null; }
        }

        internal ShieldStepResult LastResult
        {
            get { return result; }
        }

        internal Incidence Flow
        {
            get { return flow; }
        }

        internal FaceFrame Frame
        {
            get { return frame; }
        }

        /// <summary>True while the physics step runs for this shield (not packed, not under analytic integration).</summary>
        internal bool IsStepping
        {
            get { return stepping; }
        }

        internal bool IsLegacy
        {
            get { return legacyMode; }
        }

        internal string MaterialDisplayName
        {
            get { return DisplayName(); }
        }

        // ================================================================================================
        // Persistence
        // ================================================================================================

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            // B9PartSwitch also calls Load() with a subtype's DATA node or with the prefab node. Those carry
            // neither a snapshot nor a shield state and must not clear what this part already holds.
            ConfigNode holder = node.GetNode(SnapshotNodeName);
            if (holder != null && holder.nodes.Count > 0)
            {
                try
                {
                    snapshotText = CfgParser.Write(ConfigNodeBridge.ToCfg(holder.nodes[0]));
                }
                catch (Exception e)
                {
                    snapshotText = string.Empty;
                    RtpsLog.Error("stored material profile could not be read: " + e.Message);
                }
            }
            ConfigNode stored = node.GetNode(ShieldState.NodeName);
            if (stored != null)
            {
                try
                {
                    shieldStateText = CfgParser.Write(ConfigNodeBridge.ToCfg(stored));
                }
                catch (Exception e)
                {
                    RtpsLog.Error("stored shield state could not be read: " + e.Message);
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            if (HighLogic.LoadedSceneIsEditor && started && !reconciling)
            {
                // The craft file is what flight loads. Capture the final editor state (selected subtype,
                // tweaked resource amount, switcher mass) now; the field values were written before OnSave.
                CommitRequested(true);
                WriteState(node);
            }

            if (!string.IsNullOrEmpty(snapshotText))
            {
                try
                {
                    CfgNode parsed = CfgParser.Parse(snapshotText);
                    if (parsed.Nodes.Count > 0)
                    {
                        node.AddNode(SnapshotNodeName).AddNode(ConfigNodeBridge.ToConfigNode(parsed.Nodes[0]));
                    }
                }
                catch (Exception e)
                {
                    RtpsLog.Error("material profile could not be saved: " + e.Message);
                }
            }

            try
            {
                if (state != null && !stateRefused)
                {
                    node.AddNode(ConfigNodeBridge.ToConfigNode(state.Write()));
                }
                else if (!string.IsNullOrEmpty(shieldStateText))
                {
                    // Not resolved yet, or refused (damaged, or written by a newer build): write back exactly
                    // what was loaded. A refused state is never replaced by a fresh shield.
                    CfgNode parsed = CfgParser.Parse(shieldStateText);
                    if (parsed.Nodes.Count > 0)
                    {
                        node.AddNode(ConfigNodeBridge.ToConfigNode(parsed.Nodes[0]));
                    }
                }
            }
            catch (Exception e)
            {
                RtpsLog.Error("shield state could not be saved: " + e.Message);
            }
        }

        private void WriteState(ConfigNode node)
        {
            node.SetValue("rtpsStateVersion", rtpsStateVersion.ToString(CultureInfo.InvariantCulture), true);
            node.SetValue("committedMaterialId", committedMaterialId ?? string.Empty, true);
            node.SetValue("tpsInventory", tpsInventory.ToString("R", CultureInfo.InvariantCulture), true);
            node.SetValue("tpsCapacity", tpsCapacity.ToString("R", CultureInfo.InvariantCulture), true);
            node.SetValue("tpsPyrolysed", tpsPyrolysed.ToString("R", CultureInfo.InvariantCulture), true);
            node.SetValue("committedSwitcherMass", committedSwitcherMass.ToString("R", CultureInfo.InvariantCulture), true);
            node.SetValue("legacyMode", legacyMode ? "True" : "False", true);
            node.SetValue("reuseLimitExceeded", reuseLimitExceeded ? "True" : "False", true);
            node.SetValue("peakSurfaceTemp", peakSurfaceTemp.ToString("R", CultureInfo.InvariantCulture), true);
        }

        // ================================================================================================
        // Lifecycle
        // ================================================================================================

        public override void OnStart(StartState startState)
        {
            base.OnStart(startState);
            started = true;
            CaptureBaseProperties();
            if (!string.IsNullOrEmpty(charModuleName))
            {
                charModule = part.Modules.GetScalarModule(charModuleName);
            }
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

        /// <summary>Sent by B9PartSwitch after it loaded a subtype's DATA (or the prefab node) into this module.</summary>
        [KSPEvent(guiActive = false, guiActiveEditor = false, active = true)]
        public void ModuleDataChanged(BaseEventDetails details)
        {
            if (reconciling || !started)
            {
                return;
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                CommitRequested(true);
                RefreshUi();
            }
            // In flight the request is ignored: the committed profile stays.
        }

        /// <summary>Opens the shield diagnostics window on this part.</summary>
        [KSPEvent(guiActive = true, guiActiveEditor = false, guiActiveUnfocused = true, unfocusedRange = 50f, guiName = "#LOC_RTPS_ui_open_diagnostics")]
        public void OpenDiagnostics()
        {
            ShieldDiagnostics.Show(this);
        }

        // ----- editor -----

        private void StartEditor()
        {
            CommitRequested(false);
            if (ConvertLegacyCraftInventory())
            {
                CaptureInventoryFromPart();
            }
            LogLifecycle("editor");
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
            else
            {
                problem = Compatibility.Check(requested, hostClass, HostCapabilityList());
            }

            rtpsStateVersion = StateVersion;
            legacyMode = false;
            tpsPyrolysed = 0.0;
            reuseLimitExceeded = false;
            peakSurfaceTemp = 0.0;
            tpsDepleted = false;
            committedMaterialId = materialId ?? string.Empty;
            // A commit builds a new shield: nothing of an earlier flight state applies to it.
            shieldStateText = string.Empty;
            state = null;
            stateRefused = false;

            if (problem != null)
            {
                snapshotText = string.Empty;
                tpsInventory = 0.0;
                tpsCapacity = 0.0;
                committedSwitcherMass = 0.0;
                bool changed = mode != Mode.Inert || inertReason != problem;
                SetInert(problem);
                if (changed && !quiet)
                {
                    RtpsLog.Error(PartLabel() + ": " + problem);
                }
                return;
            }

            material = requested;
            mode = Mode.Active;
            inertReason = string.Empty;
            snapshotText = CfgParser.Write(MaterialWriter.Write(requested));
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
            else if (material != null && material.HasConsumable)
            {
                // No resource on the part (missing definition): fall back to the host design inventory.
                tpsCapacity = material.ConsumableArealDensity * tpsArea * thicknessFactor;
                tpsInventory = tpsCapacity;
            }
            else
            {
                tpsCapacity = 0.0;
                tpsInventory = 0.0;
            }
            B9SwitcherProxy switcher = B9SwitcherProxy.Find(part, switcherModuleId);
            committedSwitcherMass = switcher != null ? switcher.DryMass : 0.0;
        }

        /// <summary>
        /// A craft saved before RealTPS still lists the stock <c>Ablator</c>. Convert it once to the selected
        /// material (never more than the same fill fraction or the same sink energy) and drop the stock resource.
        /// </summary>
        private bool ConvertLegacyCraftInventory()
        {
            PartResource legacy = part.Resources.Get(StockAblatorResource);
            if (legacy == null || !(legacyAblatorCapacity > 0.0))
            {
                return false;
            }
            if (mode == Mode.Active && material != null && material.HasConsumable && material.ConsumableResource != StockAblatorResource)
            {
                PartResource target = part.Resources.Get(material.ConsumableResource);
                if (target != null)
                {
                    double sink = material.Ablation != null ? material.Ablation.HeatOfAblation : 0.0;
                    target.amount = InventoryTransfer.ConvertLegacy(legacy.amount, legacy.maxAmount, InventoryTransfer.StockHeatOfAblation, target.maxAmount, sink);
                    RtpsLog.Info(PartLabel() + ": legacy craft inventory " + F(legacy.amount) + "/" + F(legacy.maxAmount) + " Ablator converted to " + F(target.amount) + "/" + F(target.maxAmount) + " " + target.resourceName);
                }
            }
            part.Resources.Remove(legacy);
            return true;
        }

        // ----- flight -----

        private void StartFlight()
        {
            DisableStockAblators();
            B9SwitcherProxy switcher = B9SwitcherProxy.Find(part, switcherModuleId);

            if (!ResolveFlightProfile(switcher))
            {
                LogLifecycle("flight");
                return;
            }
            if (!ResolveShieldState())
            {
                LogLifecycle("flight");
                return;
            }

            Reconcile(switcher);
            if (!string.IsNullOrEmpty(deployAnimation))
            {
                deployModule = FindDeployModule();
            }
            ApplySurfaceProperties();
            UpdateContractFields();
            LogLifecycle("flight");
        }

        /// <summary>Decides which profile this shield flies with. Returns false when the shield stays without one.</summary>
        private bool ResolveFlightProfile(B9SwitcherProxy switcher)
        {
            if (rtpsStateVersion > 0)
            {
                if (committedMaterialId == LegacyUntreatedId)
                {
                    SetUntreated();
                    return false;
                }
                if (string.IsNullOrEmpty(snapshotText))
                {
                    SetInert(Localizer.Format("#LOC_RTPS_msg_no_profile", committedMaterialId));
                    WarnOnScreen();
                    return false;
                }

                var issues = new List<ValidationIssue>();
                MaterialDefinition parsed = null;
                MaterialParser.SnapshotStatus status;
                try
                {
                    CfgNode root = CfgParser.Parse(snapshotText);
                    status = root.Nodes.Count > 0 ? MaterialParser.ParseSnapshot(root.Nodes[0], issues, out parsed) : MaterialParser.SnapshotStatus.Invalid;
                }
                catch (Exception e)
                {
                    status = MaterialParser.SnapshotStatus.Invalid;
                    issues.Add(new ValidationIssue(true, "snapshot", e.Message));
                }

                if (status != MaterialParser.SnapshotStatus.Ok || parsed == null)
                {
                    for (int i = 0; i < issues.Count; i++)
                    {
                        RtpsLog.Error(PartLabel() + ": stored profile: " + issues[i]);
                    }
                    SetInert(Localizer.Format(status == MaterialParser.SnapshotStatus.UnsupportedVersion ? "#LOC_RTPS_msg_profile_newer" : "#LOC_RTPS_msg_profile_invalid", committedMaterialId));
                    WarnOnScreen();
                    return false;
                }

                material = parsed;
                mode = Mode.Active;
                inertReason = string.Empty;
                return true;
            }

            // No RealTPS state on this part.
            PartResource legacy = part.Resources.Get(StockAblatorResource);
            if (legacy != null && legacy.maxAmount > 0.0 && tpsArea > 0.0)
            {
                // A vessel launched before RealTPS: keep its stock ablator at the stock rate.
                material = LegacyProfile.Create(tpsArea, legacy.maxAmount, baseEmissive, baseSkinMaxTemp);
                mode = Mode.Active;
                inertReason = string.Empty;
                legacyMode = true;
                rtpsStateVersion = StateVersion;
                committedMaterialId = material.Id;
                snapshotText = CfgParser.Write(MaterialWriter.Write(material));
                tpsCapacity = legacy.maxAmount;
                tpsInventory = Math.Max(0.0, Math.Min(legacy.amount, legacy.maxAmount));
                tpsPyrolysed = tpsCapacity - tpsInventory;
                committedSwitcherMass = 0.0;
                RtpsLog.Info(PartLabel() + ": pre-RealTPS vessel, legacy stock-rate mode with " + F(tpsInventory) + "/" + F(tpsCapacity) + " Ablator");
                return true;
            }

            if (!(legacyAblatorCapacity > 0.0) && vessel != null && vessel.situation != Vessel.Situations.PRELAUNCH)
            {
                // A host that never had a stock ablator, already in flight without RealTPS state: a pre-RealTPS
                // vessel cannot be told apart from a spawned one here. Leave the part exactly as stock.
                rtpsStateVersion = StateVersion;
                committedMaterialId = LegacyUntreatedId;
                snapshotText = string.Empty;
                SetUntreated();
                RtpsLog.Info(PartLabel() + ": pre-RealTPS host without stock ablator; left with its stock thermal properties");
                return false;
            }

            // A part created without the editor (spawned by a contract or another mod): commit the request now.
            CommitRequested(false);
            if (mode != Mode.Active)
            {
                WarnOnScreen();
                return false;
            }
            if (switcher == null)
            {
                committedSwitcherMass = 0.0;
            }
            return true;
        }

        /// <summary>
        /// Reads the stored cell state, or lays the cells out for a shield that enters flight for the first
        /// time (or was committed before the cell model): uniform cells from the persisted totals, on the grid
        /// and the construction the part has now. From then on both are frozen with the shield.
        /// </summary>
        private bool ResolveShieldState()
        {
            state = null;
            stateRefused = false;
            if (!string.IsNullOrEmpty(shieldStateText))
            {
                var issues = new List<ValidationIssue>();
                ShieldState stored = null;
                ShieldState.ReadStatus status;
                try
                {
                    CfgNode root = CfgParser.Parse(shieldStateText);
                    status = root.Nodes.Count > 0 ? ShieldState.Read(root.Nodes[0], issues, out stored) : ShieldState.ReadStatus.Invalid;
                }
                catch (Exception e)
                {
                    status = ShieldState.ReadStatus.Invalid;
                    issues.Add(new ValidationIssue(true, "shield state", e.Message));
                }
                if (status != ShieldState.ReadStatus.Ok || stored == null)
                {
                    for (int i = 0; i < issues.Count; i++)
                    {
                        RtpsLog.Error(PartLabel() + ": stored shield state: " + issues[i]);
                    }
                    // The node stays as loaded and is written back unchanged.
                    stateRefused = true;
                    SetInert(Localizer.Format(status == ShieldState.ReadStatus.UnsupportedVersion ? "#LOC_RTPS_msg_state_newer" : "#LOC_RTPS_msg_state_invalid"));
                    WarnOnScreen();
                    return false;
                }
                state = stored;
            }
            else
            {
                ShieldLayout layout = legacyMode || !material.HasLayerModel ? ShieldLayout.Single() : HostLayout();
                state = ShieldState.Uniform(layout, HostConstruction(), tpsInventory, tpsCapacity, tpsPyrolysed);
                state.Seed = part.persistentId != 0u ? part.persistentId : 1u;
            }

            tpsInventory = state.TotalInventory;
            tpsCapacity = state.TotalCapacity;
            tpsPyrolysed = state.TotalPyrolysed;
            result = new ShieldStepResult(state.Count);
            recipients = new Part[state.Count];
            recipientJoules = new double[state.Count];
            routing = new ShieldRouting(state.Count);

            double[] normal, tangent;
            if (!ShieldGeometry.TryParseVector(protectedFace, out normal) || !ShieldGeometry.TryParseVector(sectorZero, out tangent)
                || !ShieldGeometry.TryMakeFrame(normal, tangent, out frame))
            {
                RtpsLog.Error(PartLabel() + ": protectedFace / sectorZero are not a usable frame; using the underside of the part");
                ShieldGeometry.TryMakeFrame(new[] { 0.0, -1.0, 0.0 }, new[] { 1.0, 0.0, 0.0 }, out frame);
            }
            return true;
        }

        private ShieldLayout HostLayout()
        {
            ShieldLayout layout = ShieldLayout.Parse(sectors.ToString(CultureInfo.InvariantCulture) + "," + rings.ToString(CultureInfo.InvariantCulture) + "," + centerPercent.ToString("R", CultureInfo.InvariantCulture));
            if (layout == null)
            {
                RtpsLog.Error(PartLabel() + ": sectors / rings / centerPercent do not form a valid grid; using the default grid");
                layout = new ShieldLayout(ShieldLayout.DefaultSectors, ShieldLayout.DefaultRings, ShieldLayout.DefaultCenterPercent);
            }
            return layout;
        }

        private ShieldConstruction HostConstruction()
        {
            var construction = new ShieldConstruction
            {
                Area = tpsArea,
                LayerThickness = Math.Max(0.0, material.DesignThickness * thicknessFactor),
                BackConductance = backConductance,
                SubstrateHeatCapacity = substrateHeatCapacity,
                StructureLimitTemp = Math.Max(0.0, structureLimitTemp),
                CarrierLimitTemp = substrateMaxTemp,
                BareEmissivity = baseEmissive > 0.0 && baseEmissive <= 1.0 ? baseEmissive : ShieldConstruction.DefaultBareEmissivity
            };
            string problem = construction.Problem();
            if (problem != null)
            {
                RtpsLog.Error(PartLabel() + ": host construction: " + problem + "; using defaults where needed");
                var defaults = new ShieldConstruction();
                if (!(construction.Area > 0.0))
                {
                    construction.Area = 1.0;
                }
                if (!(construction.BackConductance > 0.0))
                {
                    construction.BackConductance = defaults.BackConductance;
                }
                if (!(construction.SubstrateHeatCapacity > 0.0))
                {
                    construction.SubstrateHeatCapacity = defaults.SubstrateHeatCapacity;
                }
                if (!(construction.CarrierLimitTemp > 0.0))
                {
                    construction.CarrierLimitTemp = defaults.CarrierLimitTemp;
                }
            }
            return construction;
        }

        /// <summary>
        /// Brings the part in line with the committed profile after B9PartSwitch has activated whatever
        /// subtype it chose: re-selects the committed subtype when it still exists, otherwise runs detached.
        /// Then restores capacity and amount from the cells and computes the mass correction.
        /// </summary>
        private void Reconcile(B9SwitcherProxy switcher)
        {
            bool attached = false;
            if (switcher != null && !legacyMode)
            {
                string current = switcher.CurrentSubtypeName;
                if (string.Equals(current, committedMaterialId, StringComparison.Ordinal))
                {
                    attached = true;
                }
                else if (switcher.HasSubtype(committedMaterialId))
                {
                    reconciling = true;
                    try
                    {
                        attached = switcher.SwitchSubtype(committedMaterialId);
                    }
                    finally
                    {
                        reconciling = false;
                    }
                    RtpsLog.Warn(PartLabel() + ": switcher selected '" + current + "' but the shield is built from '" + committedMaterialId + "'; " + (attached ? "re-selected the committed material" : "running detached"));
                    if (attached)
                    {
                        ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_RTPS_msg_restored", part.partInfo.title, DisplayName()), 8f, ScreenMessageStyle.UPPER_CENTER);
                    }
                }
                else
                {
                    RtpsLog.Warn(PartLabel() + ": material '" + committedMaterialId + "' is no longer offered by this part; keeping the committed profile (detached)");
                }
            }

            tpsDetached = !attached;
            string resourceName = material.ConsumableResource;

            if (tpsDetached && switcher != null)
            {
                // Remove whatever the switcher created for the subtype it chose; it is not this shield.
                for (int i = part.Resources.Count - 1; i >= 0; i--)
                {
                    PartResource candidate = part.Resources[i];
                    if (candidate.resourceName != resourceName && switcher.IsManagedResource(candidate.resourceName))
                    {
                        part.Resources.Remove(candidate);
                    }
                }
            }

            mirror = null;
            resourceMissing = false;
            if (resourceName != null)
            {
                mirror = part.Resources.Get(resourceName);
                if (mirror == null)
                {
                    if (PartResourceLibrary.Instance != null && PartResourceLibrary.Instance.GetDefinition(resourceName) != null)
                    {
                        var node = new ConfigNode("RESOURCE");
                        node.AddValue("name", resourceName);
                        node.AddValue("amount", tpsInventory.ToString("R", CultureInfo.InvariantCulture));
                        node.AddValue("maxAmount", tpsCapacity.ToString("R", CultureInfo.InvariantCulture));
                        mirror = part.AddResource(node);
                    }
                    else
                    {
                        // The resource definition is gone. Keep solving on the cells and carry their mass
                        // through IPartMassModifier; never substitute another resource.
                        resourceMissing = true;
                        RtpsLog.Warn(PartLabel() + ": resource '" + resourceName + "' is not defined any more; inventory kept by RealTPS as part mass");
                    }
                }
                if (mirror != null)
                {
                    mirror.maxAmount = tpsCapacity;
                    mirror.amount = tpsInventory;
                }
            }

            double currentSwitcherMass = switcher != null ? switcher.DryMass : 0.0;
            massDelta = committedSwitcherMass - currentSwitcherMass;
            if (double.IsNaN(massDelta) || double.IsInfinity(massDelta))
            {
                massDelta = 0.0;
            }
        }

        private void DisableStockAblators()
        {
            for (int i = 0; i < part.Modules.Count; i++)
            {
                var stock = part.Modules[i] as ModuleAblator;
                if (stock != null && (stock.enabled || stock.moduleIsEnabled))
                {
                    // moduleIsEnabled alone does not stop ModuleAblator.FixedUpdate from writing conductivity.
                    stock.enabled = false;
                    stock.moduleIsEnabled = false;
                    RtpsLog.Error(PartLabel() + ": a stock ModuleAblator is present on a RealTPS host and was disabled; the part config should remove it");
                }
            }
        }

        private ModuleAnimateGeneric FindDeployModule()
        {
            for (int i = 0; i < part.Modules.Count; i++)
            {
                var animation = part.Modules[i] as ModuleAnimateGeneric;
                if (animation != null && animation.animationName == deployAnimation)
                {
                    return animation;
                }
            }
            RtpsLog.Warn(PartLabel() + ": no ModuleAnimateGeneric plays '" + deployAnimation + "'; the shield is treated as fully deployed");
            return null;
        }

        /// <summary>Deployment of an inflatable or deployable shield, 0 = stowed, 1 = open. 1 for rigid shields.</summary>
        internal double DeployProgress
        {
            get
            {
                if (deployModule == null)
                {
                    return 1.0;
                }
                float value = deployModule.GetScalar;
                return float.IsNaN(value) ? 1.0 : Mathf.Clamp01(value);
            }
        }

        private void CaptureBaseProperties()
        {
            if (baseCaptured)
            {
                return;
            }
            Part source = part.partInfo != null && part.partInfo.partPrefab != null ? part.partInfo.partPrefab : part;
            baseEmissive = source.emissiveConstant;
            baseSkinMaxTemp = source.skinMaxTemp > 0.0 ? source.skinMaxTemp : source.maxTemp;
            baseSkinInternalMult = source.skinInternalConductionMult;
            baseConductivity = source.heatConductivity;
            baseCaptured = true;
        }

        /// <summary>
        /// Writes the surface properties of the current state to the part. They are computed from the state at
        /// the end of a step and act in the engine's next step.
        /// </summary>
        private void ApplySurfaceProperties()
        {
            part.emissiveConstant = ShieldSolver.Emissivity(material, state);
            part.skinMaxTemp = ShieldSolver.NativeLimit(material, state);
            ApplyConduction(ShieldSolver.Conductance(material, state));
        }

        /// <summary>
        /// Makes the engine conduct from skin to interior like the cells' series conductance. Every engine
        /// factor is read live; when nothing can be derived the part keeps its own multiplier.
        /// </summary>
        private void ApplyConduction(double target)
        {
            double multiplier = double.NaN;
            if (target > 0.0)
            {
                double engine = ShieldSolver.EngineConductance(PhysicsGlobals.ConductionFactor, part.heatConductivity,
                    PhysicsGlobals.SkinInternalConductionFactor, PhysicsGlobals.ThermalConvergenceFactor);
                multiplier = ShieldSolver.ConductionMultiplier(target, state.Construction.Area, part.radiativeArea * part.skinExposedAreaFrac, engine);
            }
            part.skinInternalConductionMult = double.IsNaN(multiplier) ? baseSkinInternalMult : multiplier;
        }

        /// <summary>
        /// The stock ablator rule, as decompiled from <c>ModuleAblator.FixedUpdate</c>: the part keeps its own
        /// conductivity while its lower face is unoccluded and uses the reentry value otherwise.
        /// </summary>
        private void UpdateConductivity()
        {
            if (!ownsConductivity || part.DragCubes == null)
            {
                return;
            }
            float[] occluded = part.DragCubes.AreaOccluded;
            float[] weighted = part.DragCubes.WeightedArea;
            if (occluded == null || weighted == null || occluded.Length <= DownFace || weighted.Length <= DownFace)
            {
                return;
            }
            bool unoccluded = occluded[DownFace] <= 0.1 * weighted[DownFace];
            part.heatConductivity = unoccluded ? baseConductivity : reentryConductivity;
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

        /// <summary>
        /// True when this shield takes part in the step. No chemistry, damage or transfer while packed (on
        /// rails) or under analytic thermal integration.
        /// </summary>
        internal bool ReadyToStep()
        {
            return IsActive && part != null && vessel != null && FlightGlobals.ready && vessel.loaded && !vessel.packed && !part.packed
                && (double)TimeWarp.CurrentRate <= PhysicsGlobals.ThermalMaxIntegrationWarp;
        }

        internal void Idle()
        {
            stepping = false;
            sinkPowerKw = 0.0;
            breachPowerKw = 0.0;
            breachRequestKw = 0.0;
            plumePowerKw = 0.0;
            CommittedExposedKw = 0.0;
            if (plumes != null)
            {
                plumes.Reset();
            }
            tpsShearOverload = false;
            oxidising = false;
        }

        /// <summary>Phase 1: consumption, damage, the sink and the requests of exposed cells. Nothing is submitted.</summary>
        internal void Plan(double dt)
        {
            stepping = true;
            grantedBreach = 0.0;
            UpdateConductivity();

            double skinTemp = part.skinTemperature;
            if (skinTemp > peakSurfaceTemp && !double.IsNaN(skinTemp) && !double.IsInfinity(skinTemp))
            {
                peakSurfaceTemp = skinTemp;
            }

            Vector3 velocity = (Vector3)vessel.srf_velocity;
            Vector3 local = part.transform.InverseTransformDirection(velocity);
            flow = vessel.srfSpeed > 1.0 ? ShieldGeometry.Incidence(frame, local.x, local.y, local.z) : new Incidence();

            // Heat the game added to the skin from outside the airflow (engine jets): its added-skin channels
            // of the last integration, without what RealTPS itself submitted there. It only counts where a jet can
            // be traced to this part; then it shapes where consumption and surface damage land.
            double plumePower = 0.0;
            double[] plumeWeights = null;
            double addedKw = part.thermalSkinFluxPrevious + part.thermalExposedFluxPrevious - CommittedExposedKw;
            if (addedKw > 0.0 && !double.IsInfinity(addedKw) && state.Count > 1)
            {
                if (plumes == null)
                {
                    plumes = new ShieldPlumes();
                }
                plumeWeights = plumes.Weights(part, frame, FaceRadius(), state.Layout);
                if (plumeWeights != null)
                {
                    plumePower = addedKw * 1000.0;
                }
            }
            plumePowerKw = plumePower * 0.001;

            var input = new ShieldStepInput
            {
                Dt = dt,
                SkinTemp = skinTemp,
                InternalTemp = part.temperature,
                ExposedSkinHeatCapacity = ShieldCoordinator.ExposedSkinCapacity(part),
                DynamicPressure = part.dynamicPressurekPa * 1000.0,
                OxygenAtmosphere = InOxygenAtmosphere(),
                ConvectivePower = part.thermalConvectionFlux * 1000.0,
                Flow = flow,
                PlumePower = plumePower,
                PlumeWeights = plumeWeights,
                Settings = RtpsDifficulty.CurrentSettings
            };
            ShieldSolver.Step(material, state, input, result);

            tpsInventory = state.TotalInventory;
            tpsPyrolysed = state.TotalPyrolysed;
            if (mirror != null)
            {
                mirror.amount = tpsInventory;
            }
            sinkPowerKw = dt > 0.0 ? result.HeatAbsorbed * 0.001 / dt : 0.0;
            oxidising = result.OxidisedMass > 0.0;
        }

        /// <summary>The single sink of this step, J.</summary>
        internal double PlannedSink
        {
            get { return result != null ? result.HeatAbsorbed : 0.0; }
        }

        /// <summary>
        /// Phase 3: what exposed cells pass on. Requests are summed per recipient reservoir before the pair
        /// bound is applied, so the grant does not depend on how many cells the same area is divided into.
        /// </summary>
        internal void PlanTransfers(ThermalLedger<ShieldCoordinator.Reservoir> ledger, double dt)
        {
            if (!(result.BreachTotal > 0.0))
            {
                return;
            }
            var donor = new ShieldCoordinator.Reservoir(part, false);
            double donorCapacity = ShieldCoordinator.ExposedSkinCapacity(part);
            double donorTemp = part.skinTemperature;

            if (ShieldFailurePolicy.UsesOwnInterior(partRole, part.CrewCapacity > 0, part.FindModuleImplementing<ModuleCommand>() != null))
            {
                grantedBreach = ledger.Transfer(donor, donorCapacity, donorTemp,
                    new ShieldCoordinator.Reservoir(part, true), ShieldCoordinator.InteriorCapacity(part), part.temperature, result.BreachTotal, dt);
                return;
            }

            // Rays leave each exposed cell the way the heat travels: with the air relative to the craft for the
            // airflow's share, along the exhaust for the jets' share, weighted by what each passes on. With neither
            // (still air, no traced jet) they go straight through the face.
            double jetShare = Math.Max(0.0, Math.Min(1.0, result.BreachFromJets / result.BreachTotal));
            Vector3 airMotion = vessel.srfSpeed > 1.0 ? -((Vector3)vessel.srf_velocity).normalized : Vector3.zero;
            Vector3 jetMotion = plumes != null ? plumes.Direction : Vector3.zero;
            Vector3 direction = airMotion * (float)(1.0 - jetShare) + jetMotion * (float)jetShare;
            if (direction.sqrMagnitude < 1.0e-6f)
            {
                direction = -part.transform.TransformDirection(new Vector3((float)frame.Nx, (float)frame.Ny, (float)frame.Nz));
            }
            routing.Refresh(part, frame, state.Layout, direction, FaceRadius(), false);
            int count = 0;
            for (int i = 0; i < state.Count; i++)
            {
                double request = result.BreachEnergy[i];
                if (!(request > 0.0))
                {
                    continue;
                }
                Part target = routing.Target(i);
                if (target == null || target == part || target.vessel != vessel)
                {
                    continue;   // a ray that finds nothing moves nothing
                }
                int slot = 0;
                while (slot < count && recipients[slot] != target)
                {
                    slot++;
                }
                if (slot == count)
                {
                    recipients[count] = target;
                    recipientJoules[count] = 0.0;
                    count++;
                }
                recipientJoules[slot] += request;
            }
            for (int slot = 0; slot < count; slot++)
            {
                Part target = recipients[slot];
                double capacity = ShieldCoordinator.ExposedSkinCapacity(target);
                if (capacity > 0.0)
                {
                    grantedBreach += ledger.Transfer(donor, donorCapacity, donorTemp,
                        new ShieldCoordinator.Reservoir(target, false), capacity, target.skinTemperature, recipientJoules[slot], dt);
                }
                recipients[slot] = null;
            }
        }

        /// <summary>Radius of the protected face, m: from the protected area, or the opening radius of a deployable shield.</summary>
        internal double FaceRadius()
        {
            double radius = Math.Sqrt(Math.Max(0.01, state != null ? state.Construction.Area : tpsArea) / Math.PI);
            if (deployedRadius > 0.0 && deployModule != null)
            {
                double stowed = Math.Min(deployedRadius, Math.Max(0.25, 0.25 * deployedRadius));
                radius = stowed + (deployedRadius - stowed) * DeployProgress;
            }
            return radius;
        }

        /// <summary>Phase 5: surface properties for the engine's next step, flags, notifications.</summary>
        internal void Finish(double dt)
        {
            breachPowerKw = dt > 0.0 ? grantedBreach * 0.001 / dt : 0.0;
            breachRequestKw = dt > 0.0 && result != null ? result.BreachTotal * 0.001 / dt : 0.0;
            part.emissiveConstant = result.Emissivity;
            part.skinMaxTemp = result.NativeLimit;
            ApplyConduction(result.Conductance);

            if (result.ShearOverload != tpsShearOverload)
            {
                tpsShearOverload = result.ShearOverload;
                if (tpsShearOverload)
                {
                    Notify("shear_overload");
                }
            }

            if (result.NewlyExposed > 0 && !state.Failed)
            {
                Notify("cell_exposed");
                if (!breachAnnounced && material.HasLayerModel)
                {
                    breachAnnounced = true;
                    RtpsLog.Info(PartLabel() + ": first cell exposed at skin " + F(part.skinTemperature) + " K, " + F(100.0 * RemainingFraction) + " % of the inventory left");
                    if (RtpsDifficulty.Current.warnings && vessel == FlightGlobals.ActiveVessel)
                    {
                        ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_RTPS_msg_breach", part.partInfo.title), 8f, ScreenMessageStyle.UPPER_CENTER);
                    }
                }
            }

            bool depleted = material.HasConsumable && tpsInventory <= AblationSolver.DepletionThreshold;
            if (depleted && !tpsDepleted)
            {
                tpsDepleted = true;
                Notify("depleted");
                if (!material.HasLayerModel || !state.Failed)
                {
                    ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_RTPS_msg_depleted", part.partInfo.title), 8f, ScreenMessageStyle.UPPER_CENTER);
                }
                RtpsLog.Info(PartLabel() + ": thermal protection exhausted at skin " + F(part.skinTemperature) + " K");
            }

            if (!reuseLimitExceeded && material.ReuseSurfaceTemp > 0.0 && part.skinTemperature > material.ReuseSurfaceTemp)
            {
                reuseLimitExceeded = true;
                Notify("reuse_limit_exceeded");
            }

            UpdateContractFields();
            if (RtpsCatalogue.TelemetryLog)
            {
                LogTelemetry(part.skinTemperature);
            }
        }

        /// <summary>True when the structure has given way and the failure has not been enacted yet.</summary>
        internal bool FailureDue
        {
            get { return IsActive && result != null && result.FailureDue && !state.Failed; }
        }

        /// <summary>
        /// Phase 6, after every thermal transaction of the step: the shield fails, once. It stays on the vessel
        /// and loses its protection: its surface limit drops to the bare carrier's (<c>substrateMaxTemp</c>).
        /// If the bare part overheats, the game's thermal engine destroys it. RealTPS never calls
        /// <c>explode()</c>, <c>Die()</c> or <c>decouple()</c>.
        /// </summary>
        internal void EnactFailure()
        {
            bool wasActive = vessel == FlightGlobals.ActiveVessel;
            string title = part.partInfo != null ? part.partInfo.title : part.name;

            ShieldSolver.ApplyFailure(state, ShieldFailure.ProtectionLost);
            tpsInventory = state.TotalInventory;
            if (mirror != null)
            {
                mirror.amount = tpsInventory;
            }
            tpsDepleted = material.HasConsumable;
            ApplySurfaceProperties();
            UpdateContractFields();
            Notify("protection_lost");
            RtpsLog.Info(PartLabel() + ": shield failed (protection lost) at skin " + F(part.skinTemperature) + " K, exposed "
                + F(100.0 * tpsExposedArea) + " %");
            if (wasActive && RtpsDifficulty.Current.warnings)
            {
                ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_RTPS_msg_protection_lost", title), 10f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private void UpdateContractFields()
        {
            if (state == null || material == null)
            {
                return;
            }
            bool layered = material.HasLayerModel;
            tpsExposedArea = layered ? state.ExposedArea(material.HasConsumable) : 0.0;
            tpsIntegrity = layered ? state.MeanIntegrity : 1.0;
            tpsStructure = state.Structure;
            tpsFailure = state.Failure == ShieldFailure.ProtectionLost ? "protection_lost" : "none";
        }

        private bool InOxygenAtmosphere()
        {
            CelestialBody body = vessel.mainBody;
            return body != null && body.atmosphere && body.atmosphereContainsOxygen && vessel.staticPressurekPa > 0.0;
        }

        private void Notify(string stateName)
        {
            try
            {
                var details = new BaseEventDetails(BaseEventDetails.Sender.USER);
                details.Set("contractVersion", ContractVersion);
                details.Set("state", stateName);
                details.Set("materialId", committedMaterialId);
                details.Set("remainingFraction", RemainingFraction);
                details.Set("exposedArea", tpsExposedArea);
                part.SendEvent(StateEventName, details, 0);
            }
            catch (Exception e)
            {
                RtpsLog.Warn("state event '" + stateName + "' failed: " + e.Message);
            }
        }

        /// <summary>Called by the coordinator when a step threw: the shield stops instead of throwing every frame.</summary>
        internal void Fault(Exception e)
        {
            RtpsLog.Error("physics step failed on " + PartLabel() + ": " + e);
            // Keep the cells as they are: they are still saved. Only the stepping stops.
            mode = Mode.Inert;
            inertReason = "internal error: " + e.Message;
            sinkPowerKw = 0.0;
            breachPowerKw = 0.0;
            breachRequestKw = 0.0;
            stepping = false;
        }

        // ================================================================================================
        // Mass
        // ================================================================================================

        /// <summary>
        /// RealTPS adds mass only in flight, and only to keep a committed shield unchanged: the difference between
        /// the switcher mass at commit and now, plus the inventory when its resource definition is gone.
        /// </summary>
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            if (!HighLogic.LoadedSceneIsFlight || mode != Mode.Active)
            {
                return 0f;
            }
            double extra = massDelta;
            if (resourceMissing)
            {
                extra += tpsInventory * 0.001;
            }
            return (float)extra;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen()
        {
            return ModifierChangeWhen.FIXED;
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
            tpsMode = mode == Mode.Active ? "active" : (mode == Mode.Inert ? "inert" : (mode == Mode.LegacyUntreated ? "legacy_untreated" : "unresolved"));

            uiMaterial = DisplayName();
            uiStatus = StatusText();

            bool consumable = active && material.HasConsumable && tpsCapacity > 0.0;
            BaseField remaining = Fields["uiRemaining"];
            if (remaining != null)
            {
                remaining.guiActive = flight && consumable;
            }
            if (consumable)
            {
                uiRemaining = (100.0 * RemainingFraction).ToString("F0", CultureInfo.InvariantCulture) + "%  (" + (RemainingThickness * 1000.0).ToString("F1", CultureInfo.InvariantCulture) + " mm)";
            }

            bool damageState = flight && active && state != null && material.HasLayerModel;
            BaseField integrity = Fields["uiIntegrity"];
            if (integrity != null)
            {
                integrity.guiActive = damageState;
            }
            if (damageState)
            {
                uiIntegrity = Localizer.Format("#LOC_RTPS_ui_integrity_value",
                    (100.0 * tpsIntegrity).ToString("F0", CultureInfo.InvariantCulture),
                    (100.0 * tpsExposedArea).ToString("F0", CultureInfo.InvariantCulture));
            }

            BaseField limit = Fields["uiLimit"];
            if (limit != null)
            {
                limit.guiActive = active;
                limit.guiActiveEditor = active;
            }
            if (active)
            {
                double surface = flight && state != null ? ShieldSolver.NativeLimit(material, state) : material.MaxSurfaceTemp;
                uiLimit = surface.ToString("F0", CultureInfo.InvariantCulture) + " K";
                double bond = material.BondLimitTemp;
                if (structureLimitTemp > 0.0 && (bond <= 0.0 || structureLimitTemp < bond))
                {
                    bond = structureLimitTemp;
                }
                if (bond > 0.0 && (state == null || !state.Failed))
                {
                    uiLimit += " / " + Localizer.Format("#LOC_RTPS_ui_bond_limit", bond.ToString("F0", CultureInfo.InvariantCulture));
                }
            }

            BaseField sink = Fields["sinkPowerKw"];
            if (sink != null)
            {
                sink.guiActive = flight && active && PhysicsGlobals.ThermalDataDisplay;
            }
            BaseField breach = Fields["breachPowerKw"];
            if (breach != null)
            {
                breach.guiActive = flight && active && PhysicsGlobals.ThermalDataDisplay;
            }
            BaseEvent diagnostics = Events["OpenDiagnostics"];
            if (diagnostics != null)
            {
                diagnostics.guiActive = flight && active && state != null;
            }

            if (flight && active && charModule != null && material.Ablation != null && tpsCapacity > 0.0)
            {
                float scalar = charMin + (charMax - charMin) * (float)RemainingFraction;
                if (Math.Abs(scalar - lastCharScalar) > 0.002f)
                {
                    lastCharScalar = scalar;
                    try
                    {
                        charModule.SetScalar(scalar);
                    }
                    catch (Exception)
                    {
                        charModule = null;
                    }
                }
            }
        }

        private string DisplayName()
        {
            if (mode == Mode.LegacyUntreated)
            {
                return Localizer.Format("#LOC_RTPS_status_untreated");
            }
            if (legacyMode)
            {
                return Localizer.Format("#LOC_RTPS_status_legacy");
            }
            MaterialDefinition listed;
            string name;
            if (RtpsCatalogue.Current.TryGet(committedMaterialId, out listed) && !string.IsNullOrEmpty(listed.DisplayName))
            {
                name = Localizer.Format(listed.DisplayName);
            }
            else if (material != null && !string.IsNullOrEmpty(material.DisplayName))
            {
                name = Localizer.Format(material.DisplayName);
            }
            else
            {
                name = string.IsNullOrEmpty(committedMaterialId) ? materialId : committedMaterialId;
            }
            if (tpsDetached && HighLogic.LoadedSceneIsFlight && mode == Mode.Active)
            {
                name += " " + Localizer.Format("#LOC_RTPS_ui_detached");
            }
            return name;
        }

        private string StatusText()
        {
            switch (mode)
            {
                case Mode.Inert:
                    return Localizer.Format("#LOC_RTPS_status_inert", inertReason);
                case Mode.LegacyUntreated:
                    return Localizer.Format("#LOC_RTPS_status_stock");
                case Mode.Unresolved:
                    return Localizer.Format("#LOC_RTPS_status_unresolved");
            }
            if (!HighLogic.LoadedSceneIsFlight)
            {
                return Localizer.Format("#LOC_RTPS_status_ready");
            }
            if (state != null && state.Failed)
            {
                return Localizer.Format("#LOC_RTPS_status_failed");
            }
            if (state != null && material.HasLayerModel && tpsExposedArea > 0.0)
            {
                return Localizer.Format("#LOC_RTPS_status_breached", (100.0 * tpsExposedArea).ToString("F0", CultureInfo.InvariantCulture));
            }
            if (tpsDepleted)
            {
                return Localizer.Format("#LOC_RTPS_status_depleted");
            }
            if (tpsShearOverload)
            {
                return Localizer.Format("#LOC_RTPS_status_shear");
            }
            if (sinkPowerKw > 0.0)
            {
                return Localizer.Format("#LOC_RTPS_status_ablating");
            }
            if (oxidising)
            {
                return Localizer.Format("#LOC_RTPS_status_oxidising");
            }
            if (state != null && material.HasLayerModel && tpsIntegrity < 0.995)
            {
                return Localizer.Format("#LOC_RTPS_status_damaged");
            }
            if (reuseLimitExceeded)
            {
                return Localizer.Format("#LOC_RTPS_status_reuse");
            }
            return Localizer.Format("#LOC_RTPS_status_nominal");
        }

        public override string GetInfo()
        {
            return Localizer.Format("#LOC_RTPS_info", tpsArea.ToString("0.##", CultureInfo.InvariantCulture));
        }

        // ================================================================================================
        // Helpers
        // ================================================================================================

        private void SetInert(string reason)
        {
            mode = Mode.Inert;
            material = null;
            inertReason = reason ?? string.Empty;
            sinkPowerKw = 0.0;
            breachPowerKw = 0.0;
            breachRequestKw = 0.0;
        }

        private void SetUntreated()
        {
            mode = Mode.LegacyUntreated;
            material = null;
            inertReason = string.Empty;
            sinkPowerKw = 0.0;
            breachPowerKw = 0.0;
            breachRequestKw = 0.0;
        }

        private void WarnOnScreen()
        {
            RtpsLog.Error(PartLabel() + ": thermal protection inactive: " + inertReason);
            if (HighLogic.LoadedSceneIsFlight && part.partInfo != null)
            {
                ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_RTPS_msg_inert", part.partInfo.title, inertReason), 12f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private List<string> HostCapabilityList()
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(hostCapabilities))
            {
                return list;
            }
            string[] parts = hostCapabilities.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string item = parts[i].Trim();
                if (item.Length > 0)
                {
                    list.Add(item);
                }
            }
            return list;
        }

        internal string PartLabel()
        {
            string name = part != null && part.partInfo != null ? part.partInfo.name : "part";
            return name + "#" + (part != null ? part.persistentId.ToString(CultureInfo.InvariantCulture) : "?");
        }

        private static string F(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void LogLifecycle(string scene)
        {
            B9SwitcherProxy switcher = B9SwitcherProxy.Find(part, switcherModuleId);
            RtpsLog.Info("lifecycle: part=" + PartLabel() + " scene=" + scene
                + " requested=" + materialId
                + " committed=" + committedMaterialId
                + " switcher=" + (switcher != null ? switcher.CurrentSubtypeName : "none")
                + " mode=" + (mode == Mode.Active ? "active" : (mode == Mode.Inert ? "inert" : (mode == Mode.LegacyUntreated ? "legacy_untreated" : "unresolved")))
                + (legacyMode ? " legacy=stock-rate" : string.Empty)
                + (tpsDetached && mode == Mode.Active && scene == "flight" ? " detached=true" : string.Empty)
                + " inventory=" + F(tpsInventory) + "/" + F(tpsCapacity)
                + (state != null && scene == "flight" ? " grid=" + state.Layout + " cells=" + state.Count + " exposed=" + F(100.0 * tpsExposedArea) + "% failure=" + tpsFailure : string.Empty)
                + " massDelta=" + F(massDelta * 1000.0) + "kg"
                + (mode == Mode.Active && scene == "flight" ? " emissive=" + F(part.emissiveConstant) + " skinMaxTemp=" + F(part.skinMaxTemp) + " skinIntMult=" + F(part.skinInternalConductionMult) : string.Empty)
                + (mode == Mode.Inert ? " reason=\"" + inertReason + "\"" : string.Empty));
        }

        private void LogTelemetry(double skinTemp)
        {
            double now = Planetarium.GetUniversalTime();
            if (now < nextTelemetry || (skinTemp < 400.0 && sinkPowerKw <= 0.0))
            {
                return;
            }
            nextTelemetry = now + 1.0;
            double hottest = 0.0;
            for (int i = 0; i < state.Count; i++)
            {
                hottest = Math.Max(hottest, state.SubstrateTemp[i]);
            }
            RtpsLog.Info("telemetry: part=" + PartLabel()
                + " ut=" + now.ToString("F2", CultureInfo.InvariantCulture)
                + " alt=" + F(vessel.altitude)
                + " speed=" + F(vessel.srfSpeed)
                + " skinT=" + F(skinTemp)
                + " skinMax=" + F(part.skinMaxTemp)
                + " intT=" + F(part.temperature)
                + " inv=" + F(tpsInventory)
                + " pyro=" + F(tpsPyrolysed)
                + " sinkKw=" + F(sinkPowerKw)
                + " breachKw=" + F(breachPowerKw)
                + " breachReqKw=" + F(breachRequestKw)
                + " convKw=" + F(part.thermalConvectionFlux)
                + " radKw=" + F(part.thermalRadiationFlux)
                + " qPa=" + F(part.dynamicPressurekPa * 1000.0)
                + " emissive=" + F(part.emissiveConstant)
                + " condMult=" + F(part.skinInternalConductionMult)
                + " gate=" + F(result.Gate)
                + " plumeKw=" + F(plumePowerKw)
                + " plumeHits=" + (plumes != null ? plumes.Hits : 0).ToString(CultureInfo.InvariantCulture)
                + " plumeGate=" + F(result.PlumeGate)
                + " tilt=" + F(flow.Tilt)
                + " exposed=" + F(tpsExposedArea)
                + " integrity=" + F(tpsIntegrity)
                + " structure=" + F(tpsStructure)
                + " bondMaxT=" + F(hottest)
                + " shear=" + (tpsShearOverload ? "1" : "0"));
        }
    }
}
