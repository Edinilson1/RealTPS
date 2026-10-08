using System;
using RealTPS.Core.Materials;
using RealTPS.Core.Physics;

namespace RealTPS.Core.Shield
{
    /// <summary>Inputs of one step of a shield. All SI; the KSP adapter converts.</summary>
    public struct ShieldStepInput
    {
        /// <summary>Step length, s.</summary>
        public double Dt;

        /// <summary>Temperature of the exposed skin, K.</summary>
        public double SkinTemp;

        /// <summary>Internal temperature of the part, K.</summary>
        public double InternalTemp;

        /// <summary>Heat capacity of the exposed skin, J/K.</summary>
        public double ExposedSkinHeatCapacity;

        /// <summary>Free-stream dynamic pressure, Pa.</summary>
        public double DynamicPressure;

        /// <summary>True when the surrounding atmosphere contains oxygen.</summary>
        public bool OxygenAtmosphere;

        /// <summary>Convective power the engine put into the part, W. Zero or negative when it is not heating.</summary>
        public double ConvectivePower;

        /// <summary>How the flow meets the protected face.</summary>
        public Incidence Flow;
    }

    /// <summary>
    /// Result of one step. Created once per shield and reused, so that a physics step allocates nothing.
    /// Masses are kilograms, energies joules, times seconds.
    /// </summary>
    public sealed class ShieldStepResult
    {
        public double PyrolysedMass;
        public double ShearErodedMass;
        public double OxidisedMass;

        /// <summary>Inventory dropped by cells that lost their integrity, without cooling.</summary>
        public double ShedMass;

        /// <summary>Energy removed from the exposed skin by pyrolysis, J. The single sink of the step.</summary>
        public double HeatAbsorbed;

        public bool ShearOverload;

        /// <summary>The weight modulation of the airflow in use, 0..1.</summary>
        public double Gate;

        /// <summary>
        /// Heat weight per cell, area-weighted mean 1: the gated airflow weights and the gated jet weights,
        /// blended in proportion to the convective and the jet power.
        /// </summary>
        public readonly double[] Weights;

        /// <summary>Gated windward factor per cell, area-weighted mean 1.</summary>
        public readonly double[] Windward;

        /// <summary>Time each cell spent exposed during the step, s.</summary>
        public readonly double[] ExposedTime;

        /// <summary>Heat each exposed cell asks to pass on, J, already inside the donor's budget.</summary>
        public readonly double[] BreachEnergy;

        public double BreachTotal;

        /// <summary>True when the structure has given way and no failure has been recorded yet.</summary>
        public bool FailureDue;

        /// <summary>Number of cells that became exposed during the step.</summary>
        public int NewlyExposed;

        // Surface properties from the state at the end of the step; the engine sees them in its next step.

        /// <summary>Area-weighted emissivity of the face.</summary>
        public double Emissivity;

        /// <summary>Native catastrophic fallback, K.</summary>
        public double NativeLimit;

        /// <summary>Target conductance from skin to interior over the protected area, W/(m^2 K); 0 = none derived.</summary>
        public double Conductance;

        internal readonly double[] StartInventory;
        internal readonly double[] ProtectedFraction;
        internal readonly bool[] StartExposed;
        internal readonly double[] DamageRate;
        internal readonly double[] PyrolysisDemand;
        internal readonly double[] ShearDemand;
        internal readonly double[] OxidationDemand;

        public ShieldStepResult(int cells)
        {
            Weights = new double[cells];
            Windward = new double[cells];
            ExposedTime = new double[cells];
            BreachEnergy = new double[cells];
            StartInventory = new double[cells];
            ProtectedFraction = new double[cells];
            StartExposed = new bool[cells];
            DamageRate = new double[cells];
            PyrolysisDemand = new double[cells];
            ShearDemand = new double[cells];
            OxidationDemand = new double[cells];
        }

        public double ConsumedMass
        {
            get { return PyrolysedMass + ShearErodedMass + OxidisedMass + ShedMass; }
        }

        internal void Reset(int cells)
        {
            PyrolysedMass = ShearErodedMass = OxidisedMass = ShedMass = HeatAbsorbed = BreachTotal = Gate = 0.0;
            ShearOverload = FailureDue = false;
            NewlyExposed = 0;
            for (int i = 0; i < cells; i++)
            {
                Weights[i] = 1.0;
                Windward[i] = 1.0;
                ExposedTime[i] = 0.0;
                BreachEnergy[i] = 0.0;
                ProtectedFraction[i] = 1.0;
                StartExposed[i] = false;
            }
        }
    }

    /// <summary>
    /// Response of a shield, cell by cell (integration design I3-I14; design records kept outside the repository).
    /// RealTPS owns only what is computed here: the energy carried away by pyrolysed material, zero-sink erosion,
    /// an observer of the layer temperatures, damage, and the heat that exposed cells pass on. Convection,
    /// reradiation, conduction and sensible heat storage belong to the host thermal engine. The model has one
    /// surface temperature, the engine's exposed-skin node; the spatial weights allocate rates.
    /// </summary>
    public static class ShieldSolver
    {
        /// <summary>Time for surface deterioration to destroy a cell held at the native fallback, s. Calibration.</summary>
        public const double SurfaceDamageTime = 8.0;

        /// <summary>Time for a bond held at its limit to fail, s. Calibration.</summary>
        public const double BondDamageTime = 60.0;

        /// <summary>Arrhenius temperature of bond degradation, K. Calibration.</summary>
        public const double BondActivationTemp = 12000.0;

        /// <summary>Bond degradation ramps in from this far below the limit, K.</summary>
        public const double BondRampStart = 100.0;

        /// <summary>... and is at full strength this far below it, K.</summary>
        public const double BondRampEnd = 50.0;

        /// <summary>Largest factor by which the bond rate may exceed its value at the limit.</summary>
        public const double BondRateCap = 50.0;

        /// <summary>Time for fully exposed structure at twice its limit to fail, s. Calibration.</summary>
        public const double StructureDamageTime = 10.0;

        /// <summary>Share of the heat falling on an exposed cell that is passed on.</summary>
        public const double BreachShare = 0.5;

        /// <summary>Contact conductance between the skin node and what lies under it, W/(m^2 K). Calibration.</summary>
        public const double ContactConductance = 200.0;

        /// <summary>Below this thickness the layer is a massless branch: its node is not integrated, m.</summary>
        public const double NodeCutoff = 1.0e-5;

        /// <summary>Surface deterioration is not extrapolated beyond this multiple of the damage span.</summary>
        public const double SurfaceOvershootCap = 2.0;

        /// <summary>
        /// Advances a shield by one step. Invalid inputs (NaN, infinity, a non-positive step or temperature)
        /// change nothing: the solver never invents cooling or damage from bad data. Steps longer than
        /// <see cref="AblationSolver.MaxStep"/> are truncated.
        /// </summary>
        public static void Step(MaterialDefinition material, ShieldState state, ShieldStepInput input, ShieldStepResult result)
        {
            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            int n = state.Count;
            if (result.Weights.Length < n)
            {
                throw new ArgumentException("result was created for fewer cells than the state has");
            }
            result.Reset(n);

            if (!Usable(input.Dt) || !Usable(input.SkinTemp))
            {
                FillSurface(material, state, result);
                return;
            }

            ShieldLayout layout = state.Layout;
            ShieldConstruction construction = state.Construction;
            double dt = Math.Min(input.Dt, AblationSolver.MaxStep);
            double area = construction.Area;
            double capacity = Usable(input.ExposedSkinHeatCapacity) ? input.ExposedSkinHeatCapacity : 0.0;
            double skin = input.SkinTemp;
            double interior = Usable(input.InternalTemp) ? input.InternalTemp : skin;
            double power = Usable(input.ConvectivePower) ? input.ConvectivePower : 0.0;
            bool consumable = material.HasConsumable;
            bool layered = material.HasLayerModel;

            // ----- weights -----
            double[] w = result.Weights;
            double[] wind = result.Windward;
            double emissivity = Emissivity(material, state);
            ShieldWeights.Compute(layout, input.Flow.Defined ? input.Flow.Tilt : 0.0, input.Flow.Azimuth, w, wind);
            result.Gate = ShieldWeights.Gate(power, emissivity, skin, area, input.Flow.Defined ? input.Flow.Facing : 0.0);
            ShieldWeights.ApplyGate(w, n, result.Gate);
            ShieldWeights.ApplyGate(wind, n, result.Gate);

            if (layered && !state.ThermalInitialised)
            {
                for (int i = 0; i < n; i++)
                {
                    state.LayerTemp[i] = interior;
                    state.SubstrateTemp[i] = interior;
                }
                state.ThermalInitialised = true;
            }

            // ----- the moment each cell would give way, from the damage rates at the start of the step -----
            double bondLimit = construction.BondLimit(material);
            for (int i = 0; i < n; i++)
            {
                result.StartExposed[i] = state.IsExposed(i, consumable);
                result.StartInventory[i] = state.Inventory[i];
                result.DamageRate[i] = 0.0;
                double fraction = result.StartExposed[i] ? 0.0 : 1.0;
                if (layered && !result.StartExposed[i] && settings.Damage)
                {
                    double bias = CellBias(state.Seed, i, settings.CellVariation);
                    double rate = (SurfaceRate(material, skin, w[i], bias, surfaceTime) + BondRate(bondLimit * bias, state.SubstrateTemp[i], bondTime)) * Math.Max(0.0, settings.Severity);
                    if (rate > 0.0 && !double.IsInfinity(rate))
                    {
                        result.DamageRate[i] = rate;
                        fraction = Math.Min(1.0, state.Integrity[i] / (rate * dt));
                    }
                }
                result.ProtectedFraction[i] = fraction;
            }

            // ----- consumption: one rate law at the skin temperature, allocated by weight, until each cell's first event -----
            if (consumable && !state.Failed && Usable(area))
            {
                Consume(material, state, input, result, dt, area, capacity, skin);
            }

            if (layered)
            {
                // ----- damage up to the first event; a cell that gives way sheds what it still holds -----
                for (int i = 0; i < n; i++)
                {
                    double fraction = result.ProtectedFraction[i];
                    double rate = result.DamageRate[i];
                    // What the cell held until its event: the protected part of the step sees this layer, not
                    // what is left after shedding.
                    double atEvent = state.Inventory[i];
                    if (rate > 0.0)
                    {
                        double integrity = state.Integrity[i] - rate * dt * fraction;
                        if (integrity <= 1.0e-12)
                        {
                            state.Integrity[i] = 0.0;
                            result.ShedMass += state.Inventory[i];
                            state.Inventory[i] = 0.0;
                        }
                        else
                        {
                            state.Integrity[i] = integrity;
                        }
                    }

                    double protectedTime = dt * fraction;
                    double exposedTime = dt - protectedTime;
                    result.ExposedTime[i] = exposedTime;
                    if (exposedTime > 0.0 && !result.StartExposed[i])
                    {
                        result.NewlyExposed++;
                    }
                    if (protectedTime > 0.0)
                    {
                        double thickness = consumable
                            ? Thickness(material, 0.5 * (result.StartInventory[i] + atEvent), state.CellArea(i))
                            : construction.LayerThickness;
                        ObserveProtected(material, construction, thickness, protectedTime, skin, interior, ref state.LayerTemp[i], ref state.SubstrateTemp[i]);
                    }
                    if (exposedTime > 0.0)
                    {
                        ObserveExposed(construction, exposedTime, skin, interior, ref state.LayerTemp[i], ref state.SubstrateTemp[i]);
                    }
                }

                // ----- structure behind exposed cells -----
                if (!state.Failed)
                {
                    double load = 0.0;
                    double carrier = construction.CarrierLimitTemp;
                    for (int i = 0; i < n; i++)
                    {
                        if (result.ExposedTime[i] > 0.0)
                        {
                            double excess = (state.SubstrateTemp[i] - carrier) / carrier;
                            load += layout.AreaFraction(i) * Math.Max(0.0, Math.Min(3.0, excess)) * result.ExposedTime[i];
                        }
                    }
                    if (settings.Failure && load > 0.0)
                    {
                        state.Structure = Math.Max(0.0, state.Structure - Math.Max(0.0, settings.Severity) * load / structureTime);
                    }
                    result.FailureDue = settings.Failure && state.Structure <= 0.0;
                }

                // ----- heat that exposed cells pass on: a share of what the airflow and the jets put on each -----
                // The airflow counts only when it meets the protected face; a jet counts wherever it lands (its
                // power is only non-zero when it was traced to this face).
                double airPower = power > 0.0 && input.Flow.Defined && input.Flow.Facing > 0.0 ? power : 0.0;
                if (settings.Transfer && breachShare > 0.0 && airPower > 0.0 && capacity > 0.0)
                {
                    double total = 0.0;
                    for (int i = 0; i < n; i++)
                    {
                        if (result.ExposedTime[i] > 0.0)
                        {
                            double share = breachShare * layout.AreaFraction(i) * result.ExposedTime[i];
                            result.BreachEnergy[i] = share * airPower * w[i];
                            total += result.BreachEnergy[i];
                        }
                    }
                    double floor = material.Ablation != null ? material.Ablation.OnsetTemp : interior;
                    double whole = capacity * Math.Max(0.0, skin - floor);
                    double budget = whole - result.HeatAbsorbed;
                    if (!(budget > whole * 1.0e-9))
                    {
                        // The sink used the budget up (to rounding): nothing is left to pass on.
                        budget = 0.0;
                    }
                    if (total > budget)
                    {
                        double scale = total > 0.0 ? budget / total : 0.0;
                        for (int i = 0; i < n; i++)
                        {
                            result.BreachEnergy[i] *= scale;
                        }
                        total = budget;
                    }
                    result.BreachTotal = total;
                }
            }

            FillSurface(material, state, result);
        }

        /// <summary>
        /// Consumption of every cell over the part of the step before its first event (giving way, which the
        /// caller has put in <c>ProtectedFraction</c>). The channels run at constant rates over that window; a
        /// cell that empties consumes exactly what it had, shared between the channels in proportion to their
        /// rates, and is exposed from that moment. The pyrolysis rate is scaled by the largest common factor that
        /// keeps the sink inside its budget, found exactly (the sink is monotonic in that factor).
        /// </summary>
        private static void Consume(MaterialDefinition material, ShieldState state, ShieldStepInput input, ShieldStepResult result, double dt, double area, double capacity, double skin)
        {
            ShieldLayout layout = state.Layout;
            int n = state.Count;
            AblationChannel ablation = material.Ablation;
            ErosionChannel erosion = material.Erosion;
            double[] w = result.Weights;

            double pyrolysisBase = ablation != null && skin > ablation.OnsetTemp
                ? ablation.RateCoefficient * Math.Exp(-ablation.ActivationTemp / skin) * area * dt
                : 0.0;
            bool shearActive = erosion != null && erosion.ShearLimit > 0.0 && Usable(input.DynamicPressure);
            bool oxidising = erosion != null && erosion.OxidationOnsetTemp > 0.0 && input.OxygenAtmosphere && skin > erosion.OxidationOnsetTemp;
            double oxidationBase = oxidising ? erosion.OxidationRate * Math.Exp(-erosion.OxidationActivationTemp / skin) * area * dt : 0.0;

            for (int i = 0; i < n; i++)
            {
                double window = result.StartExposed[i] ? 0.0 : result.ProtectedFraction[i];
                result.PyrolysisDemand[i] = Sane(pyrolysisBase * layout.AreaFraction(i) * w[i] * window);
                result.OxidationDemand[i] = Sane(oxidationBase * layout.AreaFraction(i) * w[i] * window);
                double shear = 0.0;
                if (shearActive && window > 0.0)
                {
                    double local = input.DynamicPressure * result.Windward[i];
                    if (local > erosion.ShearLimit)
                    {
                        result.ShearOverload = true;
                        double overload = Math.Min(local / erosion.ShearLimit - 1.0, AblationSolver.MaxShearOverload);
                        shear = erosion.ShearRate * overload * area * layout.AreaFraction(i) * dt * window;
                    }
                }
                result.ShearDemand[i] = Sane(shear);
            }

            // No free cooling: the sink may not pull the skin below the onset temperature within one step.
            // Without a usable heat capacity the bound is unknown, so no sink is applied at all.
            double scale = 1.0;

            for (int i = 0; i < n; i++)
            {
                if (result.StartExposed[i])
                {
                    continue;
                }
                double inventory = state.Inventory[i];
                double pyrolysis = result.PyrolysisDemand[i] * scale;
                double shear = result.ShearDemand[i];
                double oxidation = result.OxidationDemand[i];
                double total = pyrolysis + shear + oxidation;
                double factor = 1.0;
                double remaining = inventory - total;
                double pyrolysed = pyrolysis * factor;
                state.Inventory[i] = remaining;
                state.Pyrolysed[i] += pyrolysed;
                result.PyrolysedMass += pyrolysed;
                result.ShearErodedMass += shear * factor;
                result.OxidisedMass += oxidation * factor;
                if (ablation != null)
                {
                    result.HeatAbsorbed += pyrolysed * ablation.HeatOfAblation;
                }
            }
        }

        private static double Sane(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value < 0.0 ? 0.0 : value;
        }

        // ----- the observer -----

        /// <summary>
        /// Backward-Euler step of a protected cell: a layer node and a substrate node between the skin
        /// temperature and the interior. The skin reaches the layer node through <c>L/(2k) + 1/h_x</c> and the
        /// layer node reaches the substrate through <c>L/(2k)</c>. Below <see cref="NodeCutoff"/> the layer is
        /// a massless branch of resistance <c>L/k + 1/h_x</c>: only the node is dropped, not the layer.
        /// </summary>
        public static void ObserveProtected(MaterialDefinition material, ShieldConstruction construction, double thickness, double dt,
            double skin, double interior, ref double layer, ref double substrate)
        {
            double k = material.Conductivity;
            double back = construction.BackConductance;
            double substrateCapacity = construction.SubstrateHeatCapacity / dt;
            if (!(thickness >= NodeCutoff))
            {
                double length = thickness > 0.0 ? thickness : 0.0;
                double g = 1.0 / (length / k + 1.0 / ContactConductance);
                substrate = (substrateCapacity * substrate + g * skin + back * interior) / (substrateCapacity + g + back);
                layer = substrate + (skin - substrate) * (length / (2.0 * k)) * g;
                return;
            }
            double layerCapacity = material.Density * material.SpecificHeat * thickness / dt;
            double g1 = 1.0 / (thickness / (2.0 * k) + 1.0 / ContactConductance);
            double g2 = 2.0 * k / thickness;
            double a11 = layerCapacity + g1 + g2;
            double a22 = substrateCapacity + g2 + back;
            double b1 = layerCapacity * layer + g1 * skin;
            double b2 = substrateCapacity * substrate + back * interior;
            double det = a11 * a22 - g2 * g2;
            double newLayer = (b1 * a22 + g2 * b2) / det;
            double newSubstrate = (a11 * b2 + g2 * b1) / det;
            layer = newLayer;
            substrate = newSubstrate;
        }

        /// <summary>Backward-Euler step of an exposed cell: the substrate between the skin (contact conductance) and the interior.</summary>
        public static void ObserveExposed(ShieldConstruction construction, double dt, double skin, double interior, ref double layer, ref double substrate)
        {
            double capacity = construction.SubstrateHeatCapacity / dt;
            substrate = (capacity * substrate + ContactConductance * skin + construction.BackConductance * interior)
                / (capacity + ContactConductance + construction.BackConductance);
            layer = skin;
        }

        // ----- damage laws -----

        /// <summary>
        /// Rate at which the surface of a cell deteriorates, 1/s. The weight is a calibrated spatial
        /// allocation: with all weights equal to 1 this is the rate of the whole face.
        /// </summary>
        public static double SurfaceRate(MaterialDefinition material, double skin, double weight, double bias)
        {
            return SurfaceRate(material, skin, weight, bias, SurfaceDamageTime);
        }

        /// <summary>As above, with the time constant of the difficulty in use, s.</summary>
        public static double SurfaceRate(MaterialDefinition material, double skin, double weight, double bias, double damageTime)
        {
            if (material.DamageSurfaceTemp <= 0.0 || !(damageTime > 0.0))
            {
                return 0.0;
            }
            double damage = material.DamageSurfaceTemp * bias;
            double span = material.MaxSurfaceTemp - damage;
            if (!(span > 0.0) || !(skin > damage) || !(weight > 0.0))
            {
                return 0.0;
            }
            double x = Math.Min((skin - damage) / span, SurfaceOvershootCap);
            return weight * x * x / damageTime;
        }

        /// <summary>
        /// Rate at which the bond of a cell degrades, 1/s: Arrhenius relative to the limit, with a continuous
        /// ramp below it and a cap above it.
        /// </summary>
        public static double BondRate(double limit, double substrate)
        {
            return BondRate(limit, substrate, BondDamageTime);
        }

        /// <summary>As above, with the time constant of the difficulty in use, s.</summary>
        public static double BondRate(double limit, double substrate, double damageTime)
        {
            if (!(limit > 0.0) || !(substrate > 0.0) || double.IsInfinity(limit) || double.IsInfinity(substrate) || !(damageTime > 0.0))
            {
                return 0.0;
            }
            double ramp = (substrate - (limit - BondRampStart)) / (BondRampStart - BondRampEnd);
            if (!(ramp > 0.0))
            {
                return 0.0;
            }
            if (ramp > 1.0)
            {
                ramp = 1.0;
            }
            double exponent = Math.Min(BondActivationTemp * (1.0 / limit - 1.0 / substrate), Math.Log(BondRateCap));
            return ramp * Math.Exp(exponent) / damageTime;
        }

        // ----- failure -----

        /// <summary>
        /// Records the failure of the shield as a whole, once. Every cell then counts as exposed; what the
        /// cells still held is shed without cooling. Returns the mass shed, kg.
        /// </summary>
        public static double ApplyFailure(ShieldState state, ShieldFailure mode)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (mode == ShieldFailure.None || state.Failed)
            {
                return 0.0;
            }
            double shed = 0.0;
            for (int i = 0; i < state.Count; i++)
            {
                shed += state.Inventory[i];
                state.Inventory[i] = 0.0;
            }
            state.Structure = 0.0;
            state.Failure = mode;
            return shed;
        }

        // ----- surface properties and conduction -----

        /// <summary>
        /// Emissivity of the face: the area-weighted mean of the cells, each with its own char blend, an
        /// exposed cell showing the bare carrier. A profile without a layer model has no bare cells.
        /// </summary>
        public static double Emissivity(MaterialDefinition material, ShieldState state)
        {
            bool layered = material.HasLayerModel;
            bool consumable = material.HasConsumable;
            double sum = 0.0;
            for (int i = 0; i < state.Count; i++)
            {
                double value = layered && state.IsExposed(i, consumable)
                    ? state.Construction.BareEmissivity
                    : AblationSolver.SurfaceEmissivity(material, state.Capacity[i] > 0.0 ? state.Pyrolysed[i] / state.Capacity[i] : 0.0);
                sum += value * state.Layout.AreaFraction(i);
            }
            return sum;
        }

        /// <summary>
        /// The native catastrophic fallback: the material's while some cell still protects, the bare carrier's
        /// once the shield has failed or every cell is exposed.
        /// </summary>
        public static double NativeLimit(MaterialDefinition material, ShieldState state)
        {
            if (material.HasLayerModel && (state.Failed || state.ExposedArea(material.HasConsumable) >= 1.0 - 1.0e-9))
            {
                // Failed, or nothing left that protects: the bare carrier's limit.
                return Math.Min(material.MaxSurfaceTemp, state.Construction.CarrierLimitTemp);
            }
            return material.MaxSurfaceTemp;
        }

        /// <summary>
        /// Steady conductance from skin to interior over the protected area, W/(m^2 K): the cells in parallel,
        /// each a series of layer, contact and back resistances. 0 when the profile has no layer model.
        /// </summary>
        public static double Conductance(MaterialDefinition material, ShieldState state)
        {
            if (!material.HasLayerModel)
            {
                return 0.0;
            }
            bool consumable = material.HasConsumable;
            ShieldConstruction c = state.Construction;
            double sum = 0.0;
            for (int i = 0; i < state.Count; i++)
            {
                double thickness = state.IsExposed(i, consumable) ? 0.0 : state.Thickness(i, material);
                sum += state.Layout.AreaFraction(i) / (thickness / material.Conductivity + 1.0 / ContactConductance + 1.0 / c.BackConductance);
            }
            return sum;
        }

        /// <summary>
        /// The engine's own skin-to-interior conductance per unit of exposed skin, W/(m^2 K), with a conduction
        /// multiplier of 1: <c>1000 * conductionFactor * 10 * heatConductivity * 0.5 *
        /// skinInternalConductionFactor * convergenceFactor</c> (decompiled <c>FlightIntegrator</c>).
        /// </summary>
        public static double EngineConductance(double conductionFactor, double heatConductivity, double skinInternalConductionFactor, double convergenceFactor)
        {
            return 1000.0 * conductionFactor * 10.0 * heatConductivity * 0.5 * skinInternalConductionFactor * convergenceFactor;
        }

        public const double MinConductionMultiplier = 1.0e-4;
        public const double MaxConductionMultiplier = 100.0;

        /// <summary>
        /// The value of <c>skinInternalConductionMult</c> that makes the engine conduct like the target, or NaN
        /// when none can be derived (the caller then leaves the part's own multiplier in place). The protected
        /// area never counts for more than the exposed skin the engine has.
        /// </summary>
        public static double ConductionMultiplier(double targetConductance, double protectedArea, double nativeExposedArea, double engineConductance)
        {
            if (!Usable(targetConductance) || !Usable(protectedArea) || !Usable(nativeExposedArea) || !Usable(engineConductance))
            {
                return double.NaN;
            }
            double ratio = Math.Min(1.0, protectedArea / nativeExposedArea);
            double multiplier = targetConductance * ratio / engineConductance;
            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier))
            {
                return double.NaN;
            }
            return Math.Max(MinConductionMultiplier, Math.Min(MaxConductionMultiplier, multiplier));
        }

        private static void FillSurface(MaterialDefinition material, ShieldState state, ShieldStepResult result)
        {
            result.Emissivity = Emissivity(material, state);
            result.NativeLimit = NativeLimit(material, state);
            result.Conductance = Conductance(material, state);
        }

        private static double Thickness(MaterialDefinition material, double inventory, double cellArea)
        {
            return cellArea > 0.0 && material.Density > 0.0 ? Math.Max(0.0, inventory) / (material.Density * cellArea) : 0.0;
        }

        /// <summary>Finite and strictly positive.</summary>
        private static bool Usable(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
        }
    }
}
