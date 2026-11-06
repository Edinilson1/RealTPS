# Changelog

All notable changes to RealTPS. Versions follow semantic versioning. Names of files, config nodes and prefixes are
written as they are today.

## v0.18.0 for KSP 1.12.x

 - 2026-11-05

### Changes since the previous version

* Added: C-PICA (Conformal PICA), a flight-proven felt-based ablator: lighter and a better insulator than PICA, but
  with less material to burn. On the stock rigid shields and the KV pods, at `aerospaceTech` (`aerospaceComposites`
  with Community Tech Tree).
* Added: Startup check for resources: a material whose resource has no `RESOURCE_DEFINITION` (say, a material from
  another mod that forgot to define it) is named at the main menu, since no shield can carry it.
* Changed: Difficulty is back to Friendly, Realistic, Strict and Custom, with the calibrated damage times (8 s
  surface, 60 s bond, 10 s structure) at every level. The levels differ in damage rate (0.5×, 1×, 1.5×), breach
  heat share (a quarter, half, three quarters) and variation between cells (none, 2 %, 5 %).
* Removed: The real-scale presets and `Difficulty/RealSolarSystem.cfg`. RealTPS reads the heating the game works
  out, so a bigger system's longer, hotter entries reach the shield on their own. Saves made with the old presets
  open on the level of the same name (StockRealistic and Realistic both become Realistic, and so on).
* Changed: The Brazilian Portuguese detail lines use a decimal comma.

### For Modders

* Localization is written by hand and no language is named in the code: a new language is a new
  `Localization/<language>.cfg`. Every number in a material's detail line must be one of that material's values.
* The Community Tech Tree unlocks are written by hand in `TechTree/CommunityTechTree.cfg`, one patch per material
  that moves. Another tech tree is just another `TechTree/<needs>.cfg`.

## v0.17.1 for KSP 1.12.x

 - 2026-11-02

### Changes since the previous version

* Added: The preset values live in `Difficulty/DifficultyPresets.cfg` (`RTPS_DIFFICULTY`). Players and other mods
  can retune them with ModuleManager, and a patch reaches existing saves too. Out-of-range values are clamped and
  unknown keys are ignored, each with a warning in KSP.log: a bad patch never stops the game.
* Added: A new game takes its preset from the stock difficulty level (Easy: Friendly; Normal and Moderate:
  Realistic; Hard: Strict).
* Changed: The presets come in two families: StockFriendly, StockRealistic and StockStrict for the stock system,
  and Friendly, Realistic and Strict for real-scale systems, with damage times three times longer
  (`Difficulty/RealSolarSystem.cfg`).

## v0.17.0 for KSP 1.12.x

 - 2026-10-31

### Changes since the previous version

* Added: Difficulty options per save, in a new Thermal Protection section of the game's difficulty settings:
  Friendly, Realistic, Strict and Custom. They only change the damage model, never how the materials burn or
  insulate: how fast surfaces, bonds and structures give way, how much heat gets through breaches, and switches
  for damage, shield failure and breach heat. Custom sets each value by hand.
* Added: Variation between cells: a small fixed difference between the limits of the cells of one shield, so that
  it doesn't fail everywhere at once.
* Added: A Warnings switch for the on-screen breach and failure messages.
* Added: The shield window shows the difficulty in use.
* Changed: The bond margin of a cell is measured against that cell's own limit, which can now differ from its
  neighbours'.

## v0.16.1 for KSP 1.12.x

 - 2026-10-27

### Changes since the previous version

* Changed: The shield window was redone: dark panels, summary cards at the top (mean integrity, material left, face
  exposed, structure), tabs for the views, bars that fill up towards each limit, and a tooltip with the numbers of
  the cell under the cursor. The window shrinks on screens too small for it.
* Added: A toolbar icon of its own, a shield face with one hot cell; hovering it names the window.
* Fixed: A bond already past its limit dropped out of the "bond at risk" count. An alert no longer clears by
  getting worse.
* Changed: An error in the window closes it and is logged once.

## v0.16.0 for KSP 1.12.x

 - 2026-10-24

### Changes since the previous version

* Added: The shield window, from the toolbar (flight and map view) or with Shield diagnostics in a shield's part
  menu, also on a nearby vessel within 50 m:
  - a map of the protected face seen from outside, with sector 1 at the top and the cells numbered
    counter-clockwise;
  - views for reserve, integrity, bond temperature, bond margin and heat received, smoothed while the shield is
    healthy (a note under the legend says so); a cell with an alert (exposed, critical integrity, low reserve, bond
    at risk) is drawn flat, with a hard edge and a pattern, and Grid draws every cell with its number;
  - markers on the rim for the wind and for the planet, hidden when they don't project on the face;
  - flight numbers: skin and internal temperature against their limits, dynamic pressure, surface speed, face
    tilt, cooling and breach heat;
  - the cell you click, and alert counters with the smallest bond margin of the protected cells and the worst cell
    of the view;
  - whether the data is live, or frozen on rails and in time warp, and arrows to switch between the shields of the
    vessel.
* Added: The shield shown in the window is highlighted on the craft.
* Added: The window only blocks camera and part clicks under it, so the craft stays controllable, and it hides with
  the rest of the interface (F2).
* Optimized: The window reads the shield 2.5 times a second and redraws the map only when something changed.

### Known Issues

* The map is exact to one texel: right on the edge between two cells, a click may pick the neighbouring one.

## v0.15.1 for KSP 1.12.x

 - 2026-10-17

### Changes since the previous version

* Fixed: The cooling of a step could exceed what the material could give: its limit was worked out on capped
  demands and applied to uncapped ones. The sink is now solved exactly.
* Fixed: A cell that gave way in the middle of a step kept burning for the whole step, and pyrolysis took priority
  over erosion when the material ran out. A step now runs up to each cell's first event, and the channels share
  what's left by their rates.
* Fixed: The bond estimate of a cell that gave way mid-step used the thickness left after the material was shed; it
  now uses what the cell held until that moment.
* Fixed: A shield with every cell exposed was declared failed at once, even with its structure still cold. Full
  exposure now only lowers the part's limit to that of the bare structure; failure still waits for the structure to
  give way.
* Fixed: A failure or a separation in the middle of a physics tick could make the shields of a vessel step twice in
  that tick.
* Fixed: The cells kept evolving while the game integrated heat analytically after a long gap between updates (it
  doesn't only do that at high time warp). RealTPS now reads the game's own flag.
* Fixed: The heat passed through breaches depended on how many cells the same area was split into. What the cells
  ask to pass on is summed per receiving part before the limit applies.
* Changed: A shield whose physics step throws an error stops (INACTIVE: internal error) instead of failing again
  every frame; its cells are still saved.

### Known Issues

* The switch to the game's analytic heating is noticed one physics tick late.

## v0.15.0 for KSP 1.12.x

 - 2026-10-14

### Changes since the previous version

* Added: Breaches. A cell with no material or no integrity left is exposed: it shows the bare structure, lets heat
  into the part, and passes half of the heat that falls on it to the part behind the shield, found by a ray traced
  from the cell along the airflow. On a pod, or any part with crew or a command module, that heat goes into the part
  itself.
* Added: Structure. The bare part under the exposed cells gives way, gradually, when it gets hotter than it can
  take.
* Added: Shield failure. Once the structure behind the breaches has given way, the shield fails, once. A dedicated
  shield with nothing attached below it comes off; any other part, pods and crewed parts included, stays on the
  vessel and loses its protection, and its surface limit drops to that of the bare part (1100 K for the stock rigid
  shields and pods, 700 K for the inflatable shield). RealTPS never explodes a part by itself.
* Added: Part menu: TPS integrity ("x% sound, y% of the face exposed") and Heat through breaches (kW, with the
  thermal data display on); statuses "Breached" and "FAILED - protection lost"; on-screen warnings when a shield is
  breached and when it loses its protection.
* Added: The inflatable shield's face follows its deploy animation from stowed to open. Opening and closing it
  changes its size, never its material or its damage.
* Added: Telemetry fields `exposed`, `integrity`, `structure` and `bondMaxT`.
* Changed: All the shields of a vessel take their physics step together: cooling, breach heat and surface
  properties are planned first and handed to the game once per step, and failures come last. Heat passed between
  two parts never makes them cross temperatures.

### For Modders

* Public contract version 2: `tpsExposedArea`, `tpsIntegrity`, `tpsStructure` and `tpsFailure` (`none`,
  `protection_lost`, `detached`). `OnRtpsStateChanged` adds `cell_exposed`, `protection_lost` and `detached`, and
  reports `exposedArea`.
* Host fields `partRole` (`dedicated` or `multifunction`), `deployAnimation` and `deployedRadius`.

### Known Issues

* The protected face of a KV pod is modelled as a flat disc.

## v0.14.0 for KSP 1.12.x

 - 2026-10-01

### Changes since the previous version

* Added: Integrity. The protection of each cell can now be damaged:
  - tile surfaces deteriorate above their damage limit (LI-900 1644 K, TUFROC 1870 K): a tile held past it fails
    in seconds, not in a single step;
  - the bond under each cell degrades when it stays too hot for too long, starting 100 K below its limit.
* Added: An estimate of the layer and bond temperatures of each cell, which keeps evolving after the heat pulse
  while the heat stored in the shield soaks back into the bond.
* Added: The TPS limit line also shows the bond limit (or the structure's, when that is lower), and a damaged
  shield shows "Damaged".
* Changed: Balance: ablators now end by running out or by a hot bond. PICA and Phenolic-Fiberglass on a heavy
  capsule are at the edge on a long, shallow Mun return, and their descriptions say so.

### For Modders

* Material schema (still version 1): `bondLimitTemp` (required) and `damageSurfaceTemp` (optional) added;
  `maxStructureTemp` and `depletedMaxSurfaceTemp` removed. `maxSurfaceTemp` is now the catastrophic fallback.
* Host fields `backConductance`, `substrateHeatCapacity` and `structureLimitTemp`.

## v0.13.1 for KSP 1.12.x

 - 2026-09-19

### Changes since the previous version

* Changed: The uneven wear only applies while the shield is really being heated and faces the flow: turning the
  craft without heating, or flying with the face in the lee, changes nothing.

## v0.13.0 for KSP 1.12.x

 - 2026-09-16

### Changes since the previous version

* Added: Cells. The face of each shield is split into cells: 25 on the stock parts, a centre disc and three rings
  of eight. Each cell keeps its own material, char and the heat it has received.
* Added: Uneven wear. The heat is spread over the cells by a calibrated profile, more at the centre and on the side
  facing into the flow, so a tilted shield wears out on one side first. With every cell full the total is the same
  as before, and an empty cell never draws material from another.
* Added: A shield keeps the grid and the construction it was launched with: a later change to the part config never
  reshapes a shield in flight. A stored cell state that can't be read leaves the shield INACTIVE and is saved back
  unchanged.
* Changed: Conduction into the part is worked out cell by cell.
* Changed: Vessels in the legacy stock-rate mode keep a single whole-face cell.
* Added: Telemetry fields `tilt` and `gate`.
* Optimized: A physics step allocates nothing: each shield reuses its step result.

### For Modders

* Host fields `sectors`, `rings`, `centerPercent`, `protectedFace` and `sectorZero`. The cell state is saved as
  `RTPS_SHIELD_STATE` (version 1), next to the profile snapshot.

## v0.12.0 for KSP 1.12.x

 - 2026-08-27

### Changes since the previous version

* Changed: The heat that gets through to the part is now worked out at every step from the material's conductivity
  and the thickness left, with the game's own conduction constants, instead of a hand-tuned multiplier per
  material. A layer that has burnt down lets more heat through.
* Changed: Each material's detail line shows its reference thickness and conductivity.

### For Modders

* Material schema (still version 1): `conductivity` is required; `skinInternalConductionMult` is gone.

## v0.11.0 for KSP 1.12.x

 - 2026-07-18

With this version the catalogue is complete: 17 materials in four families, 14 of them on nine stock parts
(`HeatShield0`, `HeatShield1`, `HeatShield1p5`, `HeatShield2`, `HeatShield3`, `kv1Pod`, `kv2Pod`, `kv3Pod`,
`InflatableHeatShield`) and 3 (Reinforced Carbon-Carbon, the ADEPT fabric, UHTC) waiting for a part that fits.

### Changes since the previous version

* Added: Brazilian Portuguese translation.
* Added: Optional telemetry. With `RTPS_SETTINGS { telemetryLog = true }`, KSP.log gets one line per second for
  each shield hotter than 400 K or burning material: temperatures and limits, material left, cooling, convection
  and radiation, dynamic pressure, emissivity and conduction.

## v0.10.2 for KSP 1.12.x

 - 2026-06-30

### Changes since the previous version

* Fixed: Shields on vessels that never went through the editor (spawned by a contract or another mod) commit their
  default material at launch.
* Added: One `lifecycle:` line per shield and scene in KSP.log (requested and committed material, selector, mode,
  material left, mass correction), for bug reports.

## v0.10.1 for KSP 1.12.x

 - 2026-06-23

### Changes since the previous version

* Fixed: B9PartSwitch also loads a subtype's data, and the prefab's, into the shield module; that cleared the
  profile the part had stored. It's kept now.
* Fixed: Craft files could keep the material and amount from before the last change in the editor, because the
  game writes a module's fields before its save code runs. The final editor state is captured when the craft is
  saved.

## v0.10.0 for KSP 1.12.x

 - 2026-06-20

A vessel in flight keeps what it was launched with.

### Changes since the previous version

* Added: Profile snapshot. At launch a shield stores the exact material profile it was built with, so editing,
  hiding or removing that material later doesn't change a vessel already in flight. A stored profile that is
  damaged, or was written by a newer RealTPS, leaves the shield INACTIVE with an on-screen warning; it's never
  replaced by a guess.
* Added: Vessels launched before RealTPS keep their stock Ablator at the stock rate ("Stock ablator"), and an
  inflatable shield launched before RealTPS stays exactly as stock ("Unchanged shield").
* Added: Craft saved before RealTPS convert their stock Ablator to the selected material in the editor, never with
  more protection than they had: no more than the same fill or the same cooling energy.
* Added: If B9PartSwitch picks another material for a shield in flight (after a change in the part config),
  RealTPS selects the material the shield was built from again and says so. If that material isn't offered at all
  anymore, the shield keeps flying with it, marked "(no longer offered)", and its mass is kept.
* Added: If a material's resource definition is removed, the shield keeps its material as part mass instead of
  losing it.
* Changed: B9PartSwitch is reached through a late-bound, guarded proxy: a missing or changed B9PartSwitch leaves the
  shields out of the way instead of throwing errors.

### For Modders

* Public contract version 1 on `ModuleRtpsShield`, read-only: `rtpsContractVersion`, `tpsMode` (`active`, `inert`,
  `legacy_untreated`), `tpsDepleted`, `tpsShearOverload`, `tpsDetached`, and the persisted `tpsInventory`,
  `tpsCapacity`, `tpsPyrolysed`, `reuseLimitExceeded` and `peakSurfaceTemp`.
* `OnRtpsStateChanged` part event, with `state` (`depleted`, `shear_overload`, `reuse_limit_exceeded`),
  `contractVersion`, `materialId` and `remainingFraction`.
* `schemaVersion` is optional in a catalogue profile: left out, it means the installed schema. A snapshot always
  records it, and a snapshot newer than the installed RealTPS is refused.
* The plugin declares `KSPAssembly("RealTPSRuntime")`, so other plugins can depend on it.

### Known Issues

* A snapshot can't freeze a resource definition: the specific heat and unit cost of a material's resource come
  from whatever definition is installed.

## v0.9.0 for KSP 1.12.x

 - 2026-05-28

### Changes since the previous version

* Added: Two speculative materials for late in the tech tree, both at `experimentalScience`: the fictional
  Intumescent Composite, a coating that swells into an insulating foam (the swelling itself isn't simulated), and
  the theoretical Advanced Aerogel Blanket (Gen 3) for the inflatable shield. An empty
  `GameData/RealTPSNoSpeculative` folder removes them.
* Added: Every material states its basis (historical, flight-proven, experimental, theoretical or fictional), shown
  in its detail line.
* Added: Community Tech Tree support: with it installed, Phenolic-Fiberglass unlocks at `enhancedSurvivability`,
  HEEET and the Gen 3 blanket at `aerospaceComposites`, and the Intumescent Composite at `advAerospaceEngineering`.

### For Modders

* `realismTier`. Theoretical and fictional materials are speculative: their subtypes and unlocks carry
  `NEEDS[!RealTPSNoSpeculative]`, and they can't be a part's default material.

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
