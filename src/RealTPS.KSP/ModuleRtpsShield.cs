using System;
using System.Collections.Generic;
using System.Globalization;
using RealTPS.Core.Cfg;
using RealTPS.Core.Hosts;
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
    public class ModuleRtpsShield : PartModule, IPartMassModifier
    {
        /// <summary>Persisted state layout version.</summary>
        public const int StateVersion = 1;

        /// <summary>Marker id of a pre-RealTPS host in flight that had no stock ablator; RealTPS leaves it untouched.</summary>
        public const string LegacyUntreatedId = "legacy-untreated";

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

        // ----- read-only contract and display -----

        /// <summary>True when no consumable is left anywhere on the face.</summary>
        [KSPField] public bool tpsDepleted;

        [KSPField] public bool tpsShearOverload;

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
            Inert,
            LegacyUntreated
        }

        [NonSerialized] private Mode mode = Mode.Unresolved;
        [NonSerialized] private MaterialDefinition material;
        [NonSerialized] private string inertReason = string.Empty;
        [NonSerialized] private bool started;
        [NonSerialized] private bool reconciling;
        [NonSerialized] private bool resourceMissing;
        [NonSerialized] private double massDelta;
        [NonSerialized] private PartResource mirror;
        [NonSerialized] private IScalarModule charModule;
        [NonSerialized] private float lastCharScalar = -1f;
        [NonSerialized] private bool baseCaptured;
        [NonSerialized] private double baseEmissive;
        [NonSerialized] private double baseSkinMaxTemp;
        [NonSerialized] private double baseConductivity;
        [NonSerialized] private float nextUiRefresh;
        [NonSerialized] private bool oxidising;

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

        /// <summary>Why the shield is inert; empty otherwise.</summary>
        public string InertReason
        {
            get { return inertReason; }
        }

        // ================================================================================================
        // Persistence
        // ================================================================================================

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

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
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

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

        // ----- editor -----

        private void StartEditor()
        {
            CommitRequested(false);
            if (ConvertLegacyCraftInventory())
            {
                CaptureInventoryFromPart();
            }
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
                return;
            }

            Reconcile(switcher);
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

        private void CaptureBaseProperties()
        {
            if (baseCaptured)
            {
                return;
            }
            Part source = part.partInfo != null && part.partInfo.partPrefab != null ? part.partInfo.partPrefab : part;
            baseEmissive = source.emissiveConstant;
            baseSkinMaxTemp = source.skinMaxTemp > 0.0 ? source.skinMaxTemp : source.maxTemp;
            baseConductivity = source.heatConductivity;
            baseCaptured = true;
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

        /// <summary>Phase 1: consumption, damage, the sink and the requests of exposed cells. Nothing is submitted.</summary>
        internal void Plan(double dt)
        {
            UpdateConductivity();

            double skinTemp = part.skinTemperature;
            if (skinTemp > peakSurfaceTemp && !double.IsNaN(skinTemp) && !double.IsInfinity(skinTemp))
            {
                peakSurfaceTemp = skinTemp;
            }

            var input = new ShieldStepInput
            {
                Dt = dt,
                SkinTemp = skinTemp,
                ExposedSkinHeatCapacity = ShieldCoordinator.ExposedSkinCapacity(part),
                DynamicPressure = part.dynamicPressurekPa * 1000.0,
                OxygenAtmosphere = InOxygenAtmosphere(),
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

            if (!reuseLimitExceeded && material.ReuseSurfaceTemp > 0.0 && part.skinTemperature > material.ReuseSurfaceTemp)
            {
                reuseLimitExceeded = true;
            }

        }

        private bool InOxygenAtmosphere()
        {
            CelestialBody body = vessel.mainBody;
            return body != null && body.atmosphere && body.atmosphereContainsOxygen && vessel.staticPressurekPa > 0.0;
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

            BaseField sink = Fields["sinkPowerKw"];
            if (sink != null)
            {
                sink.guiActive = flight && active && PhysicsGlobals.ThermalDataDisplay;
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
        }

        private void SetUntreated()
        {
            mode = Mode.LegacyUntreated;
            material = null;
            inertReason = string.Empty;
            sinkPowerKw = 0.0;
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
    }
}
