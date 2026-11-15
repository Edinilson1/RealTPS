# Modding reference: configuration schema (version 1)

[← Wiki home](Home.md)

This page is for modders: adding RealTPS materials to another mod's parts, adding a material, or moving the
unlocks for another tech tree. If you just play, you don't need it. Every parameter is also in
[`example.cfg`](example.cfg), commented line by line, and how the files are built (and which ones you edit by
hand) is in [CONTRIBUTING.md](../../CONTRIBUTING.md).

Three kinds of configuration exist: material profiles (`RTPS_MATERIAL`), host enrolment (a part patch) and
global settings (`RTPS_SETTINGS`). `RTPS_HOST` is an input of the patch generator and is not read by the game.
The unlock nodes of other tech trees are hand-written ModuleManager patches (section 5).

Rules that hold everywhere:

- SI units. Decimal point, invariant culture.
- Files are UTF-8 **without** a byte-order mark.
- An empty list never means "everything". A material with no permitted host class is rejected; a host with no
  material list is rejected.
- Unknown keys and unknown values are rejected, not guessed. A misspelt `techNode` must not turn into a free
  material. A rejected material is not selectable; a part whose requested
  material is rejected is inert and says so.

## 1. `RTPS_MATERIAL`

Registers a material in the catalogue. Registration never makes it selectable by itself: a host has to list
it.

```
RTPS_MATERIAL
{
	id = pica
	displayName = #LOC_RTPS_mat_pica_title
	description = #LOC_RTPS_mat_pica_desc
	family = rigid_ablative
	permittedHostClasses = rigid_disc
	density = 236
	designThickness = 0.070
	dryArealDensity = 1.0
	specificHeat = 1200
	emissivity = 0.80
	charEmissivity = 0.90
	maxSurfaceTemp = 3300
	conductivity = 0.305
	bondLimitTemp = 523
	costPerKg = 16.0
	costPerArea = 60
	techNode = metaMaterials
	unlockCost = 12000
	ABLATION
	{
		resource = RtpsPica
		onsetTemp = 750
		activationTemp = 16000
		rateCoefficient = 260
		heatOfAblation = 20.0e6
	}
	EROSION
	{
		shearLimit = 130000
		shearRate = 0.06
	}
}
```

| Key | Required | Unit | Accepted | Meaning |
|---|---|---|---|---|
| `schemaVersion` | no | — | `1` | Schema the profile is written for. **Omitted**: the schema of the installed RealTPS. **Stated**: the profile is read as exactly that version, or rejected if this RealTPS cannot read it. The shipped profiles omit it |
| `id` | yes | — | lower-case words joined by `-` | Stable identifier. Never rename a shipped id: saves and subtypes refer to it |
| `displayName`, `description` | no | — | text or `#LOC_` tag | Shown in the part window. Default: the id |
| `family` | yes | — | `rigid_ablative`, `rigid_reusable`, `flexible_insulating`, `deployable_radiative` | Decides which channels are legal |
| `speculative` | no | — | `true`, `false` | `true` for a theoretical or fictional material: the `RealTPSNoSpeculative` switch removes it from stock hosts, and it must have a `techNode`. Default `false` |
| `permittedHostClasses` | yes | — | comma list of identifiers, at least one | Host classes that may carry the material |
| `requiredHostCapabilities` | no | — | comma list | Capabilities the host must declare |
| `density` | yes | kg/m³ | 1 … 20000 | Virgin bulk density |
| `designThickness` | yes | m | 0.0005 … 1 | Reference layer thickness; with `density` it sets the consumable mass per area |
| `dryArealDensity` | no | kg/m² | 0 … 500 | Non-consumable mass per area (bond, carrier, blanket) |
| `specificHeat` | yes | J/(kg K) | 100 … 5000 | Mirrors the resource `hsp` |
| `emissivity` | yes | — | 0.01 … 1 | Virgin surface |
| `charEmissivity` | no | — | 0.01 … 1 | Default: `emissivity` |
| `charSurfaceFraction` | no | — | 0.001 … 1 | Pyrolysed share at which the surface is fully charred. Default 0.05 |
| `maxSurfaceTemp` | yes | K | 400 … 6000 | **Native catastrophic fallback**: the skin temperature at which the game destroys the part while the shield has not failed. The provenance names the criterion behind each value |
| `reuseSurfaceTemp` | no | K | ≤ `maxSurfaceTemp` | Reported once when exceeded |
| `damageSurfaceTemp` | no | K | 0, or 300 … below `maxSurfaceTemp` | Skin temperature above which the surface itself deteriorates (tiles, fabrics). 0 or omitted: no such mechanism |
| `conductivity` | yes | W/(m K) | 0.005 … 500 | Conductivity of the protection layer. Drives the layer observer and the conduction into the part |
| `bondLimitTemp` | yes | K | 300 … 6000 | Limit of the bond, or of the structure the system is attached to in its reference construction. Bond damage ramps in from 100 K below it |
| `costPerKg` | no | funds/kg | ≥ 0 | Resource unit cost |
| `costPerArea` | no | funds/m² | ≥ 0 | Construction cost |
| `techNode` | no | tech id | — | Tech node that unlocks the material (stock tree). Empty = available wherever the host is. Other tech trees move it with an override, see section 5 |
| `unlockCost` | no | funds | ≥ 0 | Entry cost of the unlock |

`ABLATION` (at most one; only for `rigid_ablative`, where it is required):

| Key | Unit | Accepted | Meaning |
|---|---|---|---|
| `resource` | — | resource name | Holds the inventory. One unit must weigh 1 kg. One resource belongs to exactly one material |
| `onsetTemp` | K | 300 … 4000 | Pyrolysis starts above this skin temperature |
| `activationTemp` | K | 100 … 100000 | `B` in `exp(-B/T)` |
| `rateCoefficient` | kg/(m² s) | > 0 | `R` |
| `heatOfAblation` | J/kg | 1e4 … 5e8 | Energy removed per kilogram pyrolysed. Excludes reradiation |

`EROSION` (at most one):

| Key | Unit | Meaning |
|---|---|---|
| `resource` | — | Wear-layer resource. Omit when an `ABLATION` node provides one; if both are present they must be equal |
| `shearLimit`, `shearRate` | Pa, kg/(m² s) | Both or neither. Loss rate at twice the limit |
| `oxidationOnsetTemp`, `oxidationActivationTemp`, `oxidationRate` | K, K, kg/(m² s) | All or none. Active only in oxygen atmospheres |

Further rules: duplicate keys are rejected; duplicate ids are rejected (the first definition wins); a
speculative material must have a `techNode`. The keys `skinInternalConductionMult`, `maxStructureTemp` and
`depletedMaxSurfaceTemp` from earlier versions are rejected: conduction is derived from `conductivity`, a blanket's
structure limit is its `bondLimitTemp`, and after a failure the limit is the host's carrier limit. So are
`realismTier`, replaced by `speculative`, and `availabilityStatus`: a material is offered exactly where a host lists
it.

### Snapshots

A vessel stores the profile it was built with as a `RTPS_MATERIAL` node inside its module data. Snapshots are
read by a separate, more tolerant reader: only numerical safety is checked (finite, correctly signed,
consistent channels), so a profile committed under older or wider limits stays readable. A snapshot always
records the `schemaVersion` it was written with, even when the catalogue profile omitted it: that is what lets
a later RealTPS migrate it. A snapshot with a higher version than the installed RealTPS is refused, not guessed.
A snapshot without `conductivity` (the synthetic profile of a vessel launched before RealTPS) has no layer model
and takes no damage.

### Shield state

Next to the snapshot a vessel stores a `RTPS_SHIELD_STATE` node (version 1): the grid (`layout = sectors,rings,
centerPercent`), the construction the shield was committed with (`area`, `layerThickness`, `backConductance`,
`substrateHeatCapacity`, `structureLimitTemp`, `carrierLimitTemp`, `bareEmissivity`), one comma list per cell
quantity (`inventory`, `capacity`, `pyrolysed`, `integrity`, `layerTemp`, `substrateTemp`, `received`),
`structure`, `failure`, `seed` and `thermalInitialised`. Grid and construction are frozen: changing them in a
part config affects shields committed afterwards, never a shield in flight. A node that cannot be read (damaged,
or written by a newer RealTPS) leaves the shield inert and is written back unchanged.

## 2. Host enrolment

A part becomes a host through a ModuleManager patch that names it exactly:

```
@PART[MyShield]:NEEDS[RealTPSRuntime&B9PartSwitch]:FOR[MyMod]
{
	!MODULE[ModuleAblator] {}
	!RESOURCE[Ablator] {}
	MODULE
	{
		name = ModuleRtpsShield
		hostClass = rigid_disc
		hostCapabilities = woven_panel_layout
		tpsArea = 4.9
		substrateMaxTemp = 1100
		partRole = dedicated
		sectors = 8
		rings = 2
		centerPercent = 2
		protectedFace = 0, -1, 0
		sectorZero = 1, 0, 0
		switcherModuleId = rtpsMaterial
		materialId = cork-phenolic
	}
	MODULE
	{
		name = ModuleB9PartSwitch
		moduleID = rtpsMaterial
		...one SUBTYPE per material...
	}
}
```

`ModuleRtpsShield` fields:

| Field | Default | Meaning |
|---|---|---|
| `hostClass` | — | Class token, matched against each material's `permittedHostClasses` |
| `hostCapabilities` | empty | Comma list, matched against `requiredHostCapabilities` |
| `tpsArea` | — | Protected area, m². Must be positive |
| `thicknessFactor` | 1 | Scales every material's design inventory on this host. Used only as a fallback: the subtype's resource capacity is what the game uses |
| `includedDryArealDensity` | 0 | Construction mass per area already contained in the part's own mass. Documentation: the subtype's `addedMass` carries the difference |
| `substrateMaxTemp` | 1100 | Surface limit of the bare carrier once the shield has failed, K |
| `partRole` | `dedicated` | Where breach heat goes: `dedicated` (a shield and nothing else: to the part behind it) or `multifunction` (a pod, a tank: to its own interior). A part with crew or a command module always uses its own interior. Failure is the same for both: the part stays and loses its protection |
| `sectors`, `rings`, `centerPercent` | 8, 2, 2 | Grid of the protected face: `sectors` (4–40) × (`rings` + 1, `rings` 0–6) bands of equal radial width, plus a centre disc holding `centerPercent` (0–100) of the area |
| `protectedFace` | `0, -1, 0` | Outward normal of the protected face, part coordinates |
| `sectorZero` | `1, 0, 0` | Direction on the face towards the middle of cell 1, part coordinates. Seen from outside, cell numbers increase counter-clockwise |
| `backConductance` | 20 | Substrate → interior, W/(m² K) |
| `substrateHeatCapacity` | 7300 | Plate behind the protection, J/(m² K) (about 3 mm of aluminium) |
| `structureLimitTemp` | 0 | Limit of the structure the protection is bonded to, K; 0 = the material's `bondLimitTemp` alone |
| `deployAnimation`, `deployedRadius` | empty, 0 | Inflatable or deployable shields: the `ModuleAnimateGeneric` animation and the open radius, m |
| `ownsConductivity` | true | Apply the stock occlusion rule to `heatConductivity`. Set false when another module owns that field |
| `reentryConductivity` | 0.01 | Value used by that rule |
| `legacyAblatorCapacity` | 0 | Stock `Ablator` the original part carried, kg. Enables legacy-craft conversion |
| `charModuleName` | empty | `ScalarModuleID` of a colour module to drive with the remaining fraction |
| `switcherModuleId` | `rtpsMaterial` | `moduleID` of the B9 switcher that selects the material |
| `materialId` | — | Baseline material: must equal the first subtype and need no unlock |

Subtype requirements (one per material): `name` = material id; `MODULE { IDENTIFIER { name =
ModuleRtpsShield } DATA { materialId = <id> } }`; `upgradeRequired = RTPS_<id with _>` except on the
baseline; for a consumable material `volumeAdded = <kg>` with `RESOURCE { name = <resource> unitsPerVolume = 1
}`; `addedMass` (t) and `addedCost` for construction; `allowSwitchInFlight = false`.

The baseline material's resource must also be on the part itself (`RESOURCE { name amount maxAmount }`), so
that B9PartSwitch selects the baseline for a new part.

## 3. `RTPS_SETTINGS`

```
RTPS_SETTINGS
{
	researchGatingWhenUpgradesDisabled = true   // default
	telemetryLog = false                        // default
}
```

| Key | Meaning |
|---|---|
| `researchGatingWhenUpgradesDisabled` | With the "part upgrades" difficulty option off in Career/Science, RealTPS materials still follow tech research. `false` restores the stock behaviour (everything but the baseline stays locked) |
| `telemetryLog` | One `KSP.log` line per second for each hot or ablating shield. For testing |

## 3a. `RTPS_DIFFICULTY` (shipped: `Difficulty/DifficultyPresets.cfg`)

The values of the three named difficulty presets (Friendly, Realistic, Strict) and the preset a new game takes for
each stock difficulty level.
A save chooses one preset in the game's difficulty options; a named preset reads its values from this node every
time the solver reads them, so a patch reaches existing saves too. `Custom` uses the save's own sliders.

```
RTPS_DIFFICULTY
{
	easy = Friendly                 // preset of a new game on the stock "Easy" level
	normal = Realistic
	moderate = Realistic
	hard = Strict
	PRESET
	{
		name = Realistic            // Friendly, Realistic, Strict
		damage = true
		damageRate = 1
		surfaceDamageTime = 8
		bondDamageTime = 60
		structureDamageTime = 10
		failure = true
		breachHeat = true
		breachShare = 0.5
		cellVariation = 0.02
	}
	// ... one PRESET per named preset
}
```

| Key | Range | Meaning |
|---|---|---|
| `damage` | true/false | Cells lose integrity to surface over-temperature and to a hot bond |
| `damageRate` | 0.25 – 3 | Multiplier on every damage rate (cells and structure) |
| `surfaceDamageTime` | 2 – 60 s | `τ_s`: time to destroy a cell whose surface is held at the native fallback |
| `bondDamageTime` | 10 – 600 s | `τ_b`: time to fail a bond held at its limit |
| `structureDamageTime` | 2 – 60 s | `τ_x`: time to fail exposed structure held at twice its limit |
| `failure` | true/false | Hot structure behind exposed cells ends in the failure of the shield |
| `breachHeat` | true/false | Exposed cells pass heat to what lies behind them |
| `breachShare` | 0 – 1 | `η`: share of the heat on an exposed cell (from the air and from engine jets) that is passed on |
| `cellVariation` | 0 – 0.10 | Fixed difference between the limits of the cells of one shield |

Every key is optional: the built-in values (identical to the shipped file) fill the gaps. Several nodes are
applied in load order, key by key. Out-of-range values are clamped and unknown names or keys are ignored, each
with a `[RealTPS]` warning in `KSP.log`; a bad patch never stops the game. `RtpsTool validate` reports the same
warnings for the shipped file.

**The levels.** Realistic is the calibrated model (`docs/sources/MISSION_TEST_MATRIX.md`). The level sets
`damageRate` (0.5 / 1 / 1.5), `breachShare` (0.25 / 0.5 / 0.75) and `cellVariation` (0 / 2 % / 5 %); the time
constants `τ_s`, `τ_b` and `τ_x` are the calibrated 8 s / 60 s / 10 s at every level.

**No preset per planet pack.** RealTPS reads the heating the game computes at every step, so a rescaled system
(Real Solar System, whose `PhysicsModifier.cfg` also changes the heating formulas) shows up on its own as longer,
hotter entries and more heat soaking into the bond. Stretching the damage times by the scale of the system would
hide exactly that.

A player or another mod can still retune a preset:

```
@RTPS_DIFFICULTY:AFTER[RealTPS]
{
	@PRESET[Realistic]
	{
		@bondDamageTime = 120
	}
}
```

## 4. `RTPS_HOST` (generator input, `data/hosts/*.cfg`)

Describes a stock host for `tools/RtpsTool generate`, which writes the part patch of section 2. Keys: `part`,
`title`, `hostClass`, `area`, `substrateMaxTemp`, `capabilities`, `materials` (ordered, baseline first),
`stockAblator`, `replacesStockAblator`, `charModuleName`, `thicknessFactor`, `includedDryArealDensity`,
`ownsConductivity`, `needs`, and the shield fields of section 2: `partRole` (required), `sectors`, `rings`,
`centerPercent`, `protectedFace`, `sectorZero`, `backConductance`, `substrateHeatCapacity`,
`structureLimitTemp`, `deployAnimation`, `deployedRadius`. Third-party mods do not need it: they write the patch directly.

A part from another mod can be enrolled here too, in its own `data/hosts/<mod>.cfg` with `needs = <ModName>`: the
generated part patch then carries `:NEEDS[RealTPSRuntime&B9PartSwitch&<ModName>]`. The unlock of a material shows the
first host without `needs`; only a material that no such host offers gets the `needs` of its hosts on its
`PARTUPGRADE` (joined with `|`), so no unlock names or requires a part that may be missing.

## 5. Other tech trees (hand-written, `TechTree/<needs>.cfg`)

A material states one node, `techNode`, for the stock tree; the generated `TechTree/MaterialUnlocks.cfg` creates
its unlock there. A tech tree mod that places things elsewhere gets one hand-written file,
`TechTree/<needs>.cfg`, named after the ModuleManager mod name that is present when the tree is installed. It
holds one patch per material **whose node changes**, and nothing else:

```
@PARTUPGRADE[RTPS_heeet]:NEEDS[RealTPSRuntime&B9PartSwitch&CommunityTechTree]:FOR[RealTPS]
{
	@techRequired = aerospaceComposites
}
```

Rules, checked by `RtpsTool validate` and a unit test (`Core/Generation/TechTreeOverrides.cs`); every
`.cfg` in `TechTree/` except `MaterialUnlocks.cfg` is read as such a file:

| Part | Rule |
|---|---|
| File name | The tree's ModuleManager mod name (letters, digits, underscore) |
| `@PARTUPGRADE[...]` | `RTPS_` + material id with `-` written as `_`; the material must exist and have a `techNode` |
| `:NEEDS[...]` | Exactly `RealTPSRuntime&B9PartSwitch&<needs>`, plus `&!RealTPSNoSpeculative` for a speculative material (as on its unlock) |
| `:FOR[...]` | `RealTPS` |
| Body | One line, `@techRequired = <node>`, a node id different from the material's `techNode` |
| File | At least one patch, each material once, no value outside a patch |

The patch applies only when that tree is installed; everything not listed keeps its `techNode`. Whether the
node exists in the other tree is **not** checked, since that tree is not in the repository. A patch for a
material that no RealTPS part offers (no RealTPS unlock) does nothing until another mod creates that unlock. A
third-party mod can ship the same kind of patch itself, without touching RealTPS.

## 6. Naming

| Thing | Convention |
|---|---|
| Material id | `lower-case-words` |
| Unlock (`PARTUPGRADE`) | `RTPS_` + id with `-` → `_` |
| RealTPS resources | `Rtps` + name; third-party resources use their own prefix |
| Localization tags | `#LOC_RTPS_mat_<id with _>_title`, `_desc`, `_detail`; `#LOC_RTPS_res_<Resource>` |
| ModuleManager pass | `:FOR[RealTPS]`; test `:NEEDS[RealTPSRuntime]` (the plugin assembly), not the folder name |
