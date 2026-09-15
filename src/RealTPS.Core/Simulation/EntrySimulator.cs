using System;
using RealTPS.Core.Materials;
using RealTPS.Core.Physics;
using RealTPS.Core.Shield;

namespace RealTPS.Core.Simulation
{
    /// <summary>Atmosphere and gravity of an entry body, reduced to what the calibration model needs.</summary>
    public sealed class EntryBody
    {
        public string Name;
        public double Radius;
        public double Mu;
        public double AtmosphereHeight;
        public bool Oxygen;

        /// <summary>Altitude (m), pressure (Pa), temperature (K) samples, ascending altitude.</summary>
        public double[] Altitude;
        public double[] Pressure;
        public double[] Temperature;

        /// <summary>Specific gas constant, J/(kg K).</summary>
        public double GasConstant = 287.053;
        public double Gamma = 1.4;

        /// <summary>
        /// Kerbin, from the commonly tabulated stock atmosphere (approximate; values recalled, not extracted
        /// from the game assets). Good to roughly +-20 % in density, which is enough to rank materials.
        /// </summary>
        public static EntryBody Kerbin()
        {
            return new EntryBody
            {
                Name = "Kerbin",
                Radius = 600000.0,
                Mu = 3.5316e12,
                AtmosphereHeight = 70000.0,
                Oxygen = true,
                Altitude = new[] { 0.0, 2500.0, 5000.0, 7500.0, 10000.0, 15000.0, 20000.0, 25000.0, 30000.0, 40000.0, 50000.0, 60000.0, 70000.0 },
                Pressure = new[] { 101325.0, 69015.0, 45625.0, 29126.0, 17934.0, 6726.0, 2549.0, 993.0, 404.0, 79.0, 15.0, 3.0, 0.01 },
                Temperature = new[] { 288.0, 272.0, 256.0, 240.0, 223.0, 216.0, 216.0, 221.0, 230.0, 250.0, 270.0, 245.0, 212.0 }
            };
        }

        public void Sample(double altitude, out double pressure, out double temperature)
        {
            if (altitude >= AtmosphereHeight)
            {
                pressure = 0.0;
                temperature = 4.0;
                return;
            }
            if (altitude <= Altitude[0])
            {
                pressure = Pressure[0];
                temperature = Temperature[0];
                return;
            }
            int i = 1;
            while (i < Altitude.Length - 1 && Altitude[i] < altitude)
            {
                i++;
            }
            double t = (altitude - Altitude[i - 1]) / (Altitude[i] - Altitude[i - 1]);
            // Pressure is close to exponential between samples; temperature is close to linear.
            pressure = Math.Exp(Math.Log(Pressure[i - 1]) + (Math.Log(Pressure[i]) - Math.Log(Pressure[i - 1])) * t);
            temperature = Temperature[i - 1] + (Temperature[i] - Temperature[i - 1]) * t;
        }
    }

    /// <summary>Vehicle and shield-part description for one simulated entry.</summary>
    public sealed class EntryCase
    {
        public string Name;
        public EntryBody Body;

        /// <summary>Entry interface speed (surface-relative), m/s, at the top of the atmosphere.</summary>
        public double EntrySpeed;

        /// <summary>Flight-path angle at the interface, degrees below horizontal (positive = descending).</summary>
        public double EntryAngleDeg;

        /// <summary>Vehicle mass excluding the shield consumable, kg.</summary>
        public double VehicleMass;

        /// <summary>Drag area Cd*A of the whole vehicle, m^2.</summary>
        public double DragArea;

        /// <summary>Shield part: protected area, m^2.</summary>
        public double ShieldArea;

        /// <summary>Shield part: total wetted (radiative) area, m^2.</summary>
        public double RadiativeArea;

        /// <summary>Shield part: structural mass, kg.</summary>
        public double ShieldDryMass;

        /// <summary>Host part values the engine uses.</summary>
        public double ReentryConductivity = 0.01;
        public double ThermalMassModifier = 1.0;
        public double SubstrateMaxTemp = 1100.0;
        public double InternalMaxTemp = 3300.0;

        // ----- shield model (ignored by the stock reference) -----

        /// <summary>Grid of the protected face.</summary>
        public ShieldLayout Layout = new ShieldLayout(ShieldLayout.DefaultSectors, ShieldLayout.DefaultRings, ShieldLayout.DefaultCenterPercent);

        /// <summary>Host thickness relative to the material's reference thickness.</summary>
        public double ThicknessFactor = 1.0;

        public double BackConductance = ShieldConstruction.DefaultBackConductance;
        public double SubstrateHeatCapacity = ShieldConstruction.DefaultSubstrateHeatCapacity;
        public double StructureLimitTemp;
        public double BareEmissivity = ShieldConstruction.DefaultBareEmissivity;

        /// <summary><c>dedicated</c> or <c>multifunction</c>.</summary>
        public string PartRole = ShieldFailurePolicy.RoleDedicated;

        /// <summary>Constant angle between the face normal and the direction of travel, degrees.</summary>
        public double TiltDeg;

        public ShieldSettings Settings = new ShieldSettings { CellVariation = 0.0 };

        /// <summary>Heat capacity of the skin of the part behind a dedicated shield, J/K (0 = 5 % of the vehicle at 800 J/(kg K)).</summary>
        public double PayloadSkinCapacity;

        /// <summary>Skin limit of the part behind a dedicated shield, K.</summary>
        public double PayloadMaxTemp = 2400.0;

        /// <summary>Seconds simulated after the entry ends, so that the heat soaking back to the bond is seen.</summary>
        public double SoakTime = 600.0;
    }

    /// <summary>Outcome of one simulated entry.</summary>
    public sealed class EntryOutcome
    {
        public bool Survived;
        public string FailureReason = string.Empty;
        public double PeakSkinTemp;
        public double PeakInternalTemp;
        public double PeakConvectiveFlux;   // W/m^2 over the protected area
        public double PeakDynamicPressure;  // Pa
        public double PeakDeceleration;     // m/s^2
        public double ConsumedMass;         // kg
        public double RemainingMass;        // kg
        public double InitialMass;          // kg
        public double HeatLoad;             // J/m^2 convective, integrated
        public double SinkEnergy;           // J absorbed by pyrolysis
        public double Duration;             // s

        // ----- shield model -----
        public double PeakBondTemp;         // K, hottest substrate under a protected cell
        public double PeakBondTime;         // s, when it happened
        public double PeakHeatingTime;      // s, when the convective flux peaked
        public double BondLimit;            // K, 0 = none
        public double MinIntegrity = 1.0;   // lowest cell integrity at the end
        public double MinFraction = 1.0;    // lowest remaining share of a cell at the end
        public double ExposedArea;          // share of the face exposed at the end
        public double Structure = 1.0;
        public ShieldFailure Failure = ShieldFailure.None;
        public double FailureTime;          // s
        public bool FailedInSoak;           // the shield failed after the entry, while the heat soaked back
        public double EntryTime;            // s, end of the entry (speed below 250 m/s under 20 km)
        public double BreachEnergy;         // J passed on by exposed cells
        public double PeakPayloadTemp;      // K
    }

    /// <summary>
    /// Calibration model: a planar point-mass entry with the stock KSP 1.12.5 convection, radiation and
    /// skin/internal conduction formulas (re-implemented from the documented <c>Physics.cfg</c> constants and
    /// the observed FlightIntegrator behaviour) around the RealTPS material solver. It is a model of the game,
    /// not the game: it ranks materials and sizes parameters, and its numbers are never reported as flight tests.
    /// </summary>
    public static class EntrySimulator
    {
        private const double Sigma = 5.670373e-8;
        private const double StandardSpecificHeat = 800.0;        // J/(kg K)
        private const double ConvergenceFactor = 0.63;
        private const double SkinInternalConductionFactor = 0.005;
        private const double ConductionFactor = 120.0;

        /// <summary>Generic stock ablator, reproduced for comparison: rate scales with capacity, no char, no clamp.</summary>
        public sealed class StockAblator
        {
            public double Capacity;            // kg
            public double LossExp = -7500.0;
            public double LossConst = 0.1;
            public double HeatOfAblation = InventoryTransfer.StockHeatOfAblation;
            public double Threshold = 500.0;
            public double Emissivity = 0.4;
            public double MaxTemp = 3300.0;
        }

        public static EntryOutcome Run(EntryCase c, MaterialDefinition material, double fillFraction = 1.0)
        {
            return RunInternal(c, material, null, fillFraction);
        }

        public static EntryOutcome RunStock(EntryCase c, StockAblator stock, double fillFraction = 1.0)
        {
            return RunInternal(c, null, stock, fillFraction);
        }

        private static EntryOutcome RunInternal(EntryCase c, MaterialDefinition material, StockAblator stock, double fillFraction)
        {
            var o = new EntryOutcome();
            const double dt = 0.02;

            double capacity = stock != null ? stock.Capacity : material.ConsumableArealDensity * c.ShieldArea * c.ThicknessFactor;
            double remaining = capacity * fillFraction;
            double pyrolysed = 0.0;
            double dryExtra = stock != null ? 0.0 : material.DryArealDensity * c.ShieldArea;
            o.InitialMass = remaining;

            double r = c.Body.Radius + c.Body.AtmosphereHeight;
            double v = c.EntrySpeed;
            double gamma = -c.EntryAngleDeg * Math.PI / 180.0;

            double skin = 300.0;
            double inner = 300.0;
            double time = 0.0;
            o.Survived = true;

            ShieldState state = null;
            ShieldStepResult step = null;
            var ledger = new ThermalLedger<int>();
            const int SkinReservoir = 0, InteriorReservoir = 1, PayloadReservoir = 2;
            double payload = 300.0;
            double payloadCapacity = c.PayloadSkinCapacity > 0.0 ? c.PayloadSkinCapacity : 0.05 * c.VehicleMass * 800.0;
            bool ownInterior = ShieldFailurePolicy.UsesOwnInterior(c.PartRole, c.PartRole == ShieldFailurePolicy.RoleMultifunction, false);
            var flow = new Incidence
            {
                Defined = true,
                Facing = Math.Cos(c.TiltDeg * Math.PI / 180.0),
                Tilt = Math.Abs(Math.Sin(c.TiltDeg * Math.PI / 180.0)),
                Azimuth = 0.0
            };
            if (stock == null)
            {
                var construction = new ShieldConstruction
                {
                    Area = c.ShieldArea,
                    LayerThickness = material.DesignThickness * c.ThicknessFactor,
                    BackConductance = c.BackConductance,
                    SubstrateHeatCapacity = c.SubstrateHeatCapacity,
                    StructureLimitTemp = c.StructureLimitTemp,
                    CarrierLimitTemp = c.SubstrateMaxTemp,
                    BareEmissivity = c.BareEmissivity
                };
                state = ShieldState.Uniform(c.Layout, construction, remaining, capacity, 0.0);
                state.Seed = 20261113u;
                step = new ShieldStepResult(state.Count);
                o.BondLimit = construction.BondLimit(material);
            }
            double soakLeft = stock == null ? c.SoakTime : 0.0;
            bool landed = false;

            while (time < 6000.0)
            {
                double altitude = r - c.Body.Radius;
                if (altitude <= 0.0)
                {
                    break;
                }
                if (landed)
                {
                    // Soak: the craft has slowed down; hold the trajectory and let the heat soak through.
                    soakLeft -= dt;
                    if (soakLeft <= 0.0)
                    {
                        break;
                    }
                    v = 100.0;
                }
                if (altitude > c.Body.AtmosphereHeight + 1000.0 && gamma > 0.0)
                {
                    o.FailureReason = "skipped out";
                    break;
                }

                double pressure, tAtm;
                c.Body.Sample(altitude, out pressure, out tAtm);
                double rho = pressure / (c.Body.GasConstant * tAtm);
                double sound = Math.Sqrt(c.Body.Gamma * c.Body.GasConstant * tAtm);
                double mach = sound > 0.0 ? v / sound : 0.0;
                double q = 0.5 * rho * v * v;

                // --- trajectory ---
                double mass = c.VehicleMass + c.ShieldDryMass + dryExtra + remaining;
                double g = c.Body.Mu / (r * r);
                double decel = q * c.DragArea / mass;
                if (!landed)
                {
                    v += (-decel - g * Math.Sin(gamma)) * dt;
                    if (v < 1.0)
                    {
                        v = 1.0;
                    }
                    gamma += (v / r - g / v) * Math.Cos(gamma) * dt;
                    r += v * Math.Sin(gamma) * dt;
                }

                // --- engine-owned thermal terms (stock formulas) ---
                double shieldMass = (c.ShieldDryMass + dryExtra) * 0.001;                 // tonnes
                double tmm = c.ThermalMassModifier;
                double skinCapacity = Math.Max(0.1, Math.Min(0.001 * c.RadiativeArea * StandardSpecificHeat * tmm, shieldMass * StandardSpecificHeat * tmm * 0.5)); // kJ/K
                double cp = stock != null ? 400.0 : material.SpecificHeat;
                double innerCapacity = Math.Max(0.1, shieldMass * StandardSpecificHeat * tmm + remaining * 0.001 * cp - skinCapacity); // kJ/K

                double areaLerp = Clamp01(-0.2 + (mach - 0.8) / (1.5 - 0.8));
                double convArea = c.RadiativeArea + (c.ShieldArea - c.RadiativeArea) * areaLerp;
                double frac = Math.Min(1.0, Math.Max(0.001, convArea / c.RadiativeArea));
                double exposedCapacity = skinCapacity * frac;                                         // kJ/K

                double machLerp = Math.Pow(Clamp01((mach - 2.0) / (5.0 - 2.0)), 3.0);
                double newtonian = (rho > 1.0 ? rho : Math.Sqrt(rho)) * (8.14 + v) * 4.0;
                double hyper = 1.0e-7 * 7.0 * (rho > 1.0 ? rho : Math.Sqrt(rho)) * v * v * v;
                double coeff = newtonian + (hyper - newtonian) * machLerp;
                double shock = Math.Max(tAtm, v + (21.0 * Math.Pow(v, 0.75) - v) * machLerp);

                double finalCoeff = coeff * convArea * 0.001;                                         // kW/K
                double pseudoRe = rho * v;
                if (pseudoRe > 100.0)
                {
                    finalCoeff *= pseudoRe > 200.0 ? 50.0 : 1.0 + 49.0 * (pseudoRe - 100.0) / 100.0;
                }
                finalCoeff = Math.Min(finalCoeff, exposedCapacity);

                double convection = (shock - skin) * finalCoeff * ConvergenceFactor;                  // kW
                double emissivity = stock != null ? stock.Emissivity : ShieldSolver.Emissivity(material, state);
                double radiation = Sigma * emissivity * (Math.Pow(skin, 4.0) - Math.Pow(tAtm, 4.0)) * c.RadiativeArea * frac * 0.001; // kW

                // Stock: the engine's own conductance. RealTPS: the derived multiplier makes the engine conduct like
                // the cells' series conductance over the protected area (never more than the exposed skin).
                double conductance = frac * c.RadiativeArea * (ConductionFactor * 10.0 * c.ReentryConductivity * 0.5) * SkinInternalConductionFactor * ConvergenceFactor; // kW/K
                if (stock == null)
                {
                    double target = ShieldSolver.Conductance(material, state);
                    double multiplier = ShieldSolver.ConductionMultiplier(target, c.ShieldArea, frac * c.RadiativeArea, conductance * 1000.0 / (frac * c.RadiativeArea));
                    if (!double.IsNaN(multiplier))
                    {
                        conductance *= multiplier;
                    }
                }
                double toInner = (skin - inner) * conductance;                                         // kW

                // --- material-owned term ---
                double sink = 0.0;                                                                     // kW
                double breach = 0.0;                                                                   // kW
                if (stock != null)
                {
                    if (skin > stock.Threshold && remaining > 0.0)
                    {
                        double loss = Math.Min(stock.LossConst * Math.Exp(stock.LossExp / skin) * stock.Capacity * dt, remaining);
                        remaining -= loss;
                        sink = loss * stock.HeatOfAblation / dt * 0.001;
                        o.SinkEnergy += loss * stock.HeatOfAblation;
                    }
                }
                else
                {
                    var input = new ShieldStepInput
                    {
                        Dt = dt,
                        SkinTemp = skin,
                        InternalTemp = inner,
                        ExposedSkinHeatCapacity = exposedCapacity * 1000.0,
                        DynamicPressure = q,
                        OxygenAtmosphere = c.Body.Oxygen,
                        ConvectivePower = convection * 1000.0,
                        Flow = flow,
                        Settings = c.Settings
                    };
                    ShieldSolver.Step(material, state, input, step);
                    remaining = state.TotalInventory;
                    pyrolysed = state.TotalPyrolysed;
                    sink = step.HeatAbsorbed / dt * 0.001;
                    o.SinkEnergy += step.HeatAbsorbed;

                    // One transaction: the sink first, then what exposed cells pass on.
                    ledger.Clear();
                    ledger.Debit(SkinReservoir, step.HeatAbsorbed);
                    if (step.BreachTotal > 0.0)
                    {
                        double granted = ownInterior
                            ? ledger.Transfer(SkinReservoir, exposedCapacity * 1000.0, skin, InteriorReservoir, innerCapacity * 1000.0, inner, step.BreachTotal, dt)
                            : ledger.Transfer(SkinReservoir, exposedCapacity * 1000.0, skin, PayloadReservoir, payloadCapacity, payload, step.BreachTotal, dt);
                        breach = granted / dt * 0.001;
                        o.BreachEnergy += granted;
                    }
                    if (step.FailureDue && !state.Failed)
                    {
                        ShieldSolver.ApplyFailure(state, ShieldFailure.ProtectionLost);
                        remaining = 0.0;
                        o.Failure = ShieldFailure.ProtectionLost;
                        o.FailureTime = time;
                        o.FailedInSoak = landed;
                    }
                }

                skin += (convection - radiation - toInner - sink - breach) * dt / exposedCapacity;
                inner += (toInner + (ownInterior ? breach : 0.0)) * dt / innerCapacity;
                if (!ownInterior)
                {
                    payload += breach * 1000.0 * dt / payloadCapacity;
                    // The part behind the shield sheds what it received as a grey body; nothing else heats it here.
                    payload -= Sigma * 0.8 * (Math.Pow(payload, 4.0) - Math.Pow(tAtm, 4.0)) * (payloadCapacity / 800.0 / 50.0) * dt / payloadCapacity;
                    o.PeakPayloadTemp = Math.Max(o.PeakPayloadTemp, payload);
                }
                if (skin < 4.0)
                {
                    skin = 4.0;
                }

                // --- bookkeeping and failure checks ---
                double fluxPerArea = convection * 1000.0 / c.ShieldArea;
                if (fluxPerArea > o.PeakConvectiveFlux)
                {
                    o.PeakHeatingTime = time;
                }
                o.PeakConvectiveFlux = Math.Max(o.PeakConvectiveFlux, fluxPerArea);
                if (state != null)
                {
                    for (int i = 0; i < state.Count; i++)
                    {
                        if (!state.IsExposed(i, material.HasConsumable) && state.SubstrateTemp[i] > o.PeakBondTemp)
                        {
                            o.PeakBondTemp = state.SubstrateTemp[i];
                            o.PeakBondTime = time;
                        }
                    }
                }
                if (convection > 0.0)
                {
                    o.HeatLoad += fluxPerArea * dt;
                }
                o.PeakSkinTemp = Math.Max(o.PeakSkinTemp, skin);
                o.PeakInternalTemp = Math.Max(o.PeakInternalTemp, inner);
                o.PeakDynamicPressure = Math.Max(o.PeakDynamicPressure, q);
                o.PeakDeceleration = Math.Max(o.PeakDeceleration, decel);

                double limit = stock != null ? stock.MaxTemp : ShieldSolver.NativeLimit(material, state);
                if (skin > limit)
                {
                    o.Survived = false;
                    bool bare = state != null && (state.Failed || state.ExposedArea(material.HasConsumable) >= 1.0 - 1.0e-9);
                    o.FailureReason = bare ? "burn-through after protection loss" : "surface over-temperature";
                    break;
                }
                if (inner > c.InternalMaxTemp)
                {
                    o.Survived = false;
                    o.FailureReason = "structure over-temperature";
                    break;
                }
                if (!ownInterior && payload > c.PayloadMaxTemp)
                {
                    o.Survived = false;
                    o.FailureReason = "payload over-temperature";
                    break;
                }

                time += dt;
                if (!landed && v < 250.0 && altitude < 20000.0)
                {
                    if (soakLeft <= 0.0)
                    {
                        break;
                    }
                    landed = true;
                    o.EntryTime = time;
                }
            }

            o.Duration = time;
            o.RemainingMass = remaining;
            o.ConsumedMass = o.InitialMass - remaining;
            if (state != null)
            {
                o.Structure = state.Structure;
                o.ExposedArea = state.ExposedArea(material.HasConsumable);
                for (int i = 0; i < state.Count; i++)
                {
                    o.MinIntegrity = Math.Min(o.MinIntegrity, state.Integrity[i]);
                    o.MinFraction = Math.Min(o.MinFraction, state.Fraction(i));
                }
            }
            return o;
        }

        private static double Clamp01(double x)
        {
            return x < 0.0 ? 0.0 : (x > 1.0 ? 1.0 : x);
        }
    }
}
