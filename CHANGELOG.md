# Changelog

All notable changes to RealTPS. Versions follow semantic versioning. Names of files, config nodes and prefixes are
written as they are today.

## v0.8.0 for KSP 1.12.x

 - 2026-05-02

### Changes since the previous version

* Added: The stock inflatable heat shield, with flexible blankets: the Ceramic-Cloth Blanket (Gen 1), available
  from the start, and the Silicon-Carbide Blanket (Gen 2, `metaMaterials`). They aren't consumed; they fail by
  temperature. The part's own mass already includes 4 kg/m² of blanket.
* Added: The ADEPT Woven Carbon Fabric joins the catalogue, with a wear layer that erodes and oxidises. It waits for
  a deployable part with ribs.

### For Modders

* Families `flexible_insulating` and `deployable_radiative`, host class `inflatable_blanket`, host field
  `includedDryArealDensity`.

## v0.7.0 for KSP 1.12.x

 - 2026-04-11

### Changes since the previous version

* Added: Making History parts: the 1.875 m heat shield, with the same materials as the 2.5 m one, and the KV-1,
  KV-2 and KV-3 pods, whose built-in heat shields take Cork-Phenolic, Phenolic-Fiberglass or Avcoat. Without the
  expansion these patches simply don't apply.
* Added: The pods carry a thinner coating than a dedicated shield (0.65 of the reference thickness) and leave the
  part's conductivity to the pod's own module.

### For Modders

* Host class `integral_pod`, host fields `thicknessFactor` and `ownsConductivity`.

## v0.6.1 for KSP 1.12.x

 - 2026-03-18

### Changes since the previous version

* Fixed: Material stripped by shear or oxidation darkened the surface as if it had charred. Only material that
  burns (pyrolysis) makes char now.

## v0.6.0 for KSP 1.12.x

 - 2026-03-14

### Changes since the previous version

* Added: Erosion, which removes material without cooling the shield:
  - shear, above a dynamic-pressure limit: Cork-Phenolic 30 kPa, SLA-561V 20 kPa, SIRCA-15 35 kPa, Avcoat 90 kPa,
    PICA 130 kPa, HEEET 500 kPa; status "Eroding (dynamic pressure too high)";
  - oxidation when very hot, only in an atmosphere with oxygen (Kerbin, Laythe), going by the same flag the game
    uses for jet engines; status "Oxidising".
* Added: Reinforced Carbon-Carbon and UHTC (ZrB2-SiC) join the catalogue, with a thin coating or oxide scale that
  oxidises away. No stock part fits them yet, so they can't be selected.

### For Modders

* `EROSION` node, with shear and/or oxidation. A material has one consumable inventory: `EROSION.resource` must be
  the `ABLATION.resource` when both exist.
* `availabilityStatus = DEFERRED_UNTIL_COMPATIBLE_HOST`: a profile that is loaded and validated but that no part
  offers.

## v0.5.0 for KSP 1.12.x

 - 2026-02-25

### Changes since the previous version

* Added: Reusable surfaces, a kind of protection that isn't consumed: the surface glows, radiates the heat away and
  insulates what's behind it.
  - LI-900 Silica Tile (`hypersonicFlight`): superb insulation, a low temperature limit.
  - TUFROC (`aerospaceTech`): a toughened carbon cap that takes more heat than plain tiles, at a much higher mass
    and cost.
* Added: A reuse limit. Going past it marks the shield ("Reuse limit exceeded") and records the peak surface
  temperature; it does no damage.
* Changed: The TPS remaining line only shows for materials that are consumed.

### For Modders

* Family `rigid_reusable`: only rigid ablators may declare an `ABLATION` node. New keys `reuseSurfaceTemp`,
  `maxStructureTemp` (the part's internal limit) and `depletedMaxSurfaceTemp` (the surface limit once the material
  is gone).

## v0.4.1 for KSP 1.12.x

 - 2026-02-07

### Changes since the previous version

* Fixed: A stock `ModuleAblator` left on an enrolled shield by another mod's patch is switched off completely
  (disabling the module alone didn't stop it from writing the part's conductivity), and the startup check names
  the part.
* Changed: RealTPS applies the stock ablator's conductivity rule itself: a shield keeps its own conductivity while
  its underside is uncovered, and the low reentry value while something covers it.

## v0.4.0 for KSP 1.12.x

 - 2026-02-02

### Changes since the previous version

* Added: SIRCA-15, light ceramic tiles for the two small shields (0.625 m and 1.25 m), at `composites`.
* Added: HEEET, a woven carbon ablator for the 1.25 m shield and up, at `aerospaceTech`.
* Added: A material can require something of the part (small tiles, woven panels), so it's only offered where it
  fits.
* Added: Startup check. At the main menu, a RealTPS window lists what's wrong: ModuleManager or B9PartSwitch
  missing, material profiles rejected, an enrolled part set up wrong. It shows up to eight problems; the rest are
  in KSP.log. RealTPS never repairs a configuration silently.
* Added: A shield whose material isn't valid for its part is INACTIVE, with the reason in the part menu and an
  on-screen warning, instead of being silently protected.
* Added: KSP.log notes how many materials were loaded, whether research gating is on and how many parts are
  enrolled.

### For Modders

* `permittedHostClasses` and `requiredHostCapabilities` on materials, `hostClass` and `hostCapabilities` on parts.
  An empty list never means "all", and wildcards aren't accepted.
* Another mod can enrol its own heat shield with a ModuleManager patch that names the part: remove the stock
  ablator, add `ModuleRtpsShield` and a B9PartSwitch selector with one subtype per material.

## v0.3.1 for KSP 1.12.x

 - 2026-01-13

### Changes since the previous version

* Fixed: Research gating answers both questions the game asks about an upgrade: whether it's unlocked and whether
  it's enabled.
* Fixed: Research gating is installed again at every scene change, so it keeps working when another mod replaces
  the game's upgrade handler after RealTPS.

## v0.3.0 for KSP 1.12.x

 - 2026-01-10

### Changes since the previous version

* Added: Materials are unlocked through the tech tree, each with a stock part upgrade on its node, an entry cost,
  its own title and description and the shield's icon: Phenolic-Fiberglass at `survivability`, Avcoat at
  `landing`, SLA-561V at `advLanding`, Carbon-Phenolic at `heavyLanding`, PICA at `metaMaterials`. Cork-Phenolic
  needs no unlock.
* Added: Research still gates the materials when the "part upgrades" difficulty option is off in Career or
  Science; without that, only Cork-Phenolic would ever be available. `researchGatingWhenUpgradesDisabled = false`
  in `RTPS_SETTINGS` restores the stock behaviour.
* Added: Material costs and fixed mass: the consumable is priced per kilogram, and the fixed construction (bond,
  honeycomb, backing) adds mass and cost per square metre.

### For Modders

* `techNode` and `unlockCost` per material; each unlock is a `PARTUPGRADE` named `RTPS_<id>`. The default material
  of a part needs no unlock.

## v0.2.1 for KSP 1.12.x

 - 2025-12-19

### Changes since the previous version

* Changed: The cooling of one step is limited to the heat stored in the skin above the onset temperature: RealTPS
  alone never cools the surface below it, and applies no cooling at all when the skin's heat capacity can't be
  read.
* Changed: Bad readings from the game (NaN, infinity, a zero-length step) make a step do nothing instead of
  producing cooling.

## v0.2.0 for KSP 1.12.x

 - 2025-12-13

### Changes since the previous version

* Added: SLA-561V, Carbon-Phenolic and PICA, on all four shields.
* Added: Char. The surface emissivity changes from the virgin material's to the char's as the material burns, and
  the stock shields darken as they lose material.
* Added: An Ablation cooling line (kW) in the part menu while the game's thermal data display is on.
* Added: The part info in the editor shows the protected area, and that the material can't be changed in flight.
* Added: Each material has a description and a detail line in the selector.

## v0.1.2 for KSP 1.12.x

 - 2025-12-02

### Changes since the previous version

* Fixed: Reloading the game database from the debug menu threw a `NullReferenceException`; the materials now
  reload with it.
* Changed: A physics step longer than one second (a lag spike) is cut to one second: less burning and less
  cooling, never more.

## v0.1.1 for KSP 1.12.x

 - 2025-11-27

### Changes since the previous version

* Fixed: The stock `ModuleAblator` is removed from enrolled shields, not only its resource: it initialises only in
  flight and kept writing the part's conductivity even with nothing to ablate.
* Fixed: Enrolled shields no longer charge for the stock Ablator they don't carry anymore.

## v0.1.0 for KSP 1.12.x

 - 2025-11-25

First version, then called Kerbal Ablative Materials: the one generic Ablator of the stock heat shields is replaced
by a choice of real ablators.

* Added: Cork-Phenolic, Phenolic-Fiberglass and Avcoat Honeycomb on the four stock heat shields (0.625 m, 1.25 m,
  2.5 m and 3.75 m), chosen in the editor with a Thermal protection selector. Cork-Phenolic is the default, and
  the material can't be changed in flight.
* Added: The material that burns away carries the heat with it: above an onset temperature the shield's skin loses
  energy at a rate that grows with temperature, never more than the material left can take. The game still does
  the heating, the radiation, the conduction and the heat storage.
* Added: One resource per material, replacing the stock Ablator: 1 unit = 1 kg, and it doesn't flow or transfer
  between parts.
* Added: Part menu lines: TPS material, TPS status (Ready, Nominal, Ablating, Exhausted), TPS remaining (percentage
  and thickness in mm) and TPS limit, the surface temperature at which the game destroys the part.
* Added: An on-screen warning when a shield's material runs out.
* Added: English localization.
* Requires ModuleManager and B9PartSwitch. Without them the heat shields keep their stock behaviour.

### For Modders

* Materials are `RTPS_MATERIAL` nodes (schema version 1), read after ModuleManager. A profile with any error is
  rejected and logged, never filled in with invented defaults, and unknown or duplicate keys are errors.
* Parts are enrolled by name only, each with its own `@PART[...]` patch. Nothing is matched by wildcard.
* Every RealTPS line in KSP.log starts with `[RealTPS]`.
