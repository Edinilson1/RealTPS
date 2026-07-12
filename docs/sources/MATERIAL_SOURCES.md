# Material sources

Where each number in `GameData/RealTPS/Materials/*.cfg` comes from. I write this file by hand. If you change a
number in a material config, change its row here too: `RtpsTool validate` fails when a material number has no
row here, when a row has a different value, or when a row doesn't match anything.

## How to read a row

| Status | Meaning |
|---|---|
| `SOURCED` | The value is the published value (rounded at most to the precision shown) |
| `DERIVED` | Computed from sourced values by the rule in the note |
| `ASSUMPTION` | Engineering assumption; no primary source was verified for this exact number |
| `CALIBRATION` | Gameplay calibration: tuned in the numerical mission model so that the material lands in its intended niche; anchored to the literature only by ordering |
| `FICTIONAL` | Invented for a fictional or theoretical material |

Source is a key from the table below, or `-` when there is none. The research notes are kept outside the
repository. "Not verified" means just that: nobody in this project has confirmed it.

## Sources

| Key | Source | Link |
|---|---|---|
| `tpsx_avcoat` | NASA TPSX Material Properties Database - Avcoat 5026-39 HC/G | <https://tpsx.arc.nasa.gov/Material?id=37&units=si> |
| `tpsx_sla561v` | NASA TPSX Material Properties Database - SLA-561V | <https://tpsx.arc.nasa.gov/Material?id=39> |
| `tpsx_sirca` | NASA TPSX Material Properties Database - SIRCA-15F | <https://tpsx.arc.nasa.gov/Material?id=40&units=si> |
| `tpsx_pica` | NASA TPSX Material Properties Database - PICA | <https://tpsx.arc.nasa.gov/Material?id=43&units=si> |
| `tpsx_li900` | NASA TPSX Material Properties Database - LI-900 | <https://tpsx.arc.nasa.gov/Material?id=1&units=si> |
| `tpsx_rcc` | NASA TPSX Material Properties Database - Reinforced Carbon-Carbon (coated, TEOS, Type A) | <https://tpsx.arc.nasa.gov/Material?id=24> |
| `amorim_tps` | Amorim Cork Solutions - aerospace TPS brochure (P45 / P50 phenolic cork) | <https://amorimcorksolutions.com/media/1095/en-aerospace_brochure_general_356001.pdf> |
| `nasa_carbon_phenolic` | NASA carbon-phenolic report (Galileo FM5055G virgin and char density) | <https://ntrs.nasa.gov/api/citations/20200003199/downloads/20200003199.pdf> |
| `nasa_heeet` | NASA - Heatshield for Extreme Entry Environment Technology nears maturity | <https://science.nasa.gov/science-research/science-enabling-technology/technology-highlights/heatshield-for-extreme-entry-environment-technology-nears-maturity/> |
| `nasa_ftps_tests` | NASA - flexible TPS aerothermal testing for HIAD (0.2-0.5 MW/m2 at 3-8 kPa) | <https://ntrs.nasa.gov/api/citations/20120011663/downloads/20120011663.pdf> |
| `nasa_loftid` | NASA - LOFTID and HIAD overview (layup: SiC fabric, carbon felt, aerogel, gas barrier) | <https://ntrs.nasa.gov/api/citations/20210018646/downloads/EDL%20Summer%20Series%20-%20LOFTID%20%26%20HIAD_Final_noVids.pdf> |
| `nasa_adept` | NASA - ADEPT 3-D woven carbon cloth arc-jet study | <https://ntrs.nasa.gov/api/citations/20130011056/downloads/20130011056.pdf> |
| `nasa_uhtc` | NASA - ultra-high-temperature ceramic (ZrB2-SiC family) arc-jet study | <https://ntrs.nasa.gov/api/citations/20040111983/downloads/20040111983.pdf> |
| `iop_starlite` | Institute of Physics - Enigma: Starlite (uncertain historical claims) | <https://spark.iop.org/enigma-starlite> |
| `tpsx_uhtc` | NASA TPSX Material Properties Database - ZrB2-20% SiC | <https://tpsx.arc.nasa.gov/Material?id=28&units=si> |
| `ksp_stock` | Kerbal Space Program 1.12.5 part configs and decompiled ModuleAblator (stock reference: 2.4 MJ/kg, 0.1*exp(-7500/T)) | The game files themselves (not redistributable) |
| `model` | RealTPS numerical mission model (tools/RtpsTool simulate); see docs/sources/MISSION_TEST_MATRIX.md | `docs/sources/MISSION_TEST_MATRIX.md` |

## Materials

### `adept-carbon-fabric`: Woven Carbon Fabric (ADEPT)

deployable_radiative, Experimental.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 880 | kg/m3 | ASSUMPTION | - | assumed for 3-D woven carbon cloth; not verified |
| designThickness | 0.00254 | m | SOURCED | `nasa_adept` | initial test cloth thickness 0.00254 m |
| dryArealDensity | 1 | kg/m2 | ASSUMPTION | - | non-consumable bond, carrier, coating or blanket mass per area; not measured |
| specificHeat | 1100 | J/(kg K) | ASSUMPTION | - | not verified |
| emissivity | 0.9 | - | ASSUMPTION | `nasa_adept` | 0.9 is a modelling assumption in the cited study, not a measurement |
| charEmissivity | 0.9 | - | ASSUMPTION | `nasa_adept` | no char: equal to the emissivity |
| maxSurfaceTemp | 2400 | K | CALIBRATION | `nasa_adept` | native catastrophic fallback. Criterion: placed above the reradiation temperature of the highest tested point (2.46 MW/m2 at 9.6 kPa); no host exists yet to calibrate against |
| costPerKg | 220 | funds/kg | CALIBRATION | - | gameplay cost |
| costPerArea | 300 | funds/m2 | CALIBRATION | - | gameplay cost |
| unlockCost | 26000 | funds | CALIBRATION | - | gameplay cost |
| erosion.shearLimit | 60000 | Pa | CALIBRATION | `model` | dynamic pressure above which char or fabric is removed mechanically; ordered by reported pressure capability |
| erosion.shearRate | 0.02 | kg/(m2 s) | CALIBRATION | `model` | areal loss rate at twice the shear limit |
| erosion.oxidationOnsetTemp | 1100 | K | ASSUMPTION | - | temperature above which the wear layer oxidises in an oxygen atmosphere |
| erosion.oxidationActivationTemp | 14000 | K | CALIBRATION | - | Arrhenius temperature of the lumped oxidation law |
| erosion.oxidationRate | 9 | kg/(m2 s) | CALIBRATION | - | pre-exponential areal oxidation rate |
| techNode | aerospaceTech | tech node id | CALIBRATION | - | progression design: unlock node in the stock tech tree |
| techNode@CommunityTechTree | aerospaceComposites | tech node id | CALIBRATION | - | progression design: node used when that tech tree is installed (hand-written TechTree/<needs>.cfg) |

### `ftps-nextel-gen1`: Ceramic-Cloth Blanket (Gen 1)

flexible_insulating, FlightProven.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 250 | kg/m3 | ASSUMPTION | `nasa_loftid` | assumed bulk value for the layup; installed blanket density is not published |
| designThickness | 0.016 | m | ASSUMPTION | - | assumed blanket thickness; it sets no inventory |
| dryArealDensity | 4 | kg/m2 | ASSUMPTION | - | non-consumable bond, carrier, coating or blanket mass per area; not measured |
| specificHeat | 1000 | J/(kg K) | ASSUMPTION | - | not verified |
| emissivity | 0.85 | - | ASSUMPTION | - | not verified for the outer ceramic cloth |
| charEmissivity | 0.85 | - | ASSUMPTION | - | no char: equal to the emissivity |
| maxSurfaceTemp | 1800 | K | DERIVED | `nasa_ftps_tests` | temperature at which the surface reradiates the highest tested heat flux for this class: eps*sigma*T^4 = 0.5 MW/m2 gives about 1800 K. A flux-equivalent limit, higher than the fabric's real service temperature |
| costPerArea | 20 | funds/m2 | CALIBRATION | - | gameplay cost |

### `ftps-sic-gen2`: Silicon-Carbide Blanket (Gen 2)

flexible_insulating, FlightProven.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 300 | kg/m3 | ASSUMPTION | `nasa_loftid` | assumed bulk value for the layup |
| designThickness | 0.02 | m | ASSUMPTION | - | assumed; it sets no inventory |
| dryArealDensity | 6 | kg/m2 | ASSUMPTION | - | non-consumable bond, carrier, coating or blanket mass per area; not measured |
| specificHeat | 1000 | J/(kg K) | ASSUMPTION | - | not verified |
| emissivity | 0.88 | - | ASSUMPTION | - | not verified for the SiC outer fabric |
| charEmissivity | 0.88 | - | ASSUMPTION | - | no char: equal to the emissivity |
| maxSurfaceTemp | 1970 | K | ASSUMPTION | `nasa_loftid` | native catastrophic fallback. Criterion: flux-equivalent limit for an assumed 0.75 MW/m2 class (eps*sigma*T^4), about 1970 K; the 0.75 MW/m2 figure is not verified |
| costPerArea | 55 | funds/m2 | CALIBRATION | - | gameplay cost |
| unlockCost | 14000 | funds | CALIBRATION | - | gameplay cost |
| techNode | metaMaterials | tech node id | CALIBRATION | - | progression design: unlock node in the stock tech tree |

### `ftps-aerogel-gen3`: Advanced Aerogel Blanket (Gen 3, theoretical)

flexible_insulating, Theoretical.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 180 | kg/m3 | FICTIONAL | - | theoretical third-generation layup; no such blanket has flown |
| designThickness | 0.018 | m | FICTIONAL | - | theoretical; it sets no inventory |
| dryArealDensity | 3.2 | kg/m2 | FICTIONAL | - | theoretical |
| specificHeat | 1000 | J/(kg K) | FICTIONAL | - | theoretical |
| emissivity | 0.9 | - | FICTIONAL | - | theoretical |
| charEmissivity | 0.9 | - | FICTIONAL | - | theoretical |
| maxSurfaceTemp | 2150 | K | FICTIONAL | `model` | theoretical: flux-equivalent limit for an assumed 1.1 MW/m2 class |
| costPerArea | 160 | funds/m2 | CALIBRATION | - | gameplay cost |
| unlockCost | 30000 | funds | CALIBRATION | - | gameplay cost |
| techNode | experimentalScience | tech node id | CALIBRATION | - | progression design: unlock node in the stock tech tree |
| techNode@CommunityTechTree | aerospaceComposites | tech node id | CALIBRATION | - | progression design: node used when that tech tree is installed (hand-written TechTree/<needs>.cfg) |

### `cork-phenolic`: Cork-Phenolic

rigid_ablative, Historical.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 480 | kg/m3 | SOURCED | `amorim_tps` | P50 grade about 480 kg/m3 (brochure range 448-512) |
| designThickness | 0.06 | m | CALIBRATION | `model` | reference layer thickness; sized in the mission model for the material's intended missions on stock shields |
| specificHeat | 2100 | J/(kg K) | SOURCED | `amorim_tps` | about 2100 J/(kg K) for the P50 grade (supplier typical value) |
| emissivity | 0.7 | - | ASSUMPTION | - | not verified for phenolic cork |
| charEmissivity | 0.85 | - | ASSUMPTION | - | charred surfaces are treated as darker than the virgin surface; no formulation-specific measurement verified |
| charSurfaceFraction | 0.05 | - | CALIBRATION | `model` | share of the inventory pyrolysed before the surface counts as fully charred |
| maxSurfaceTemp | 1900 | K | CALIBRATION | `model` | native catastrophic fallback. Criterion: gameplay calibration of the lumped KSP skin node, placed so the material is destroyed in the flux class where the literature says it stops being used; not a measured decomposition temperature |
| costPerKg | 1 | funds/kg | CALIBRATION | - | gameplay cost |
| ablation.onsetTemp | 550 | K | CALIBRATION | `model` | skin temperature at which the lumped pyrolysis law switches on |
| ablation.activationTemp | 8000 | K | CALIBRATION | `model` | Arrhenius temperature of the lumped areal rate law (stock uses 7500 K) |
| ablation.rateCoefficient | 13 | kg/(m2 s) | CALIBRATION | `model` | pre-exponential areal rate of the lumped law |
| ablation.heatOfAblation | 6000000 | J/kg | CALIBRATION | `model` | incremental sink per kilogram actually removed in the KSP energy balance. NOT the TPSX 'effective heat of ablation', which includes reradiation and a backface criterion (stock generic ablator: 2.4 MJ/kg) |
| erosion.shearLimit | 30000 | Pa | CALIBRATION | `model` | dynamic pressure above which char or fabric is removed mechanically; ordered by reported pressure capability |
| erosion.shearRate | 0.05 | kg/(m2 s) | CALIBRATION | `model` | areal loss rate at twice the shear limit |

### `phenolic-fiberglass`: Phenolic-Fiberglass

rigid_ablative, Historical.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 1750 | kg/m3 | ASSUMPTION | - | typical glass-cloth/phenolic laminate; the Mercury-era value was not verified |
| designThickness | 0.028 | m | CALIBRATION | `model` | reference layer thickness; sized in the mission model for the material's intended missions on stock shields |
| specificHeat | 1250 | J/(kg K) | ASSUMPTION | - | typical glass/phenolic laminate; not verified |
| emissivity | 0.8 | - | ASSUMPTION | - | not verified |
| charEmissivity | 0.88 | - | ASSUMPTION | - | charred surfaces are treated as darker than the virgin surface; no formulation-specific measurement verified |
| charSurfaceFraction | 0.05 | - | CALIBRATION | `model` | share of the inventory pyrolysed before the surface counts as fully charred |
| maxSurfaceTemp | 2300 | K | CALIBRATION | `model` | native catastrophic fallback. Criterion: gameplay calibration of the lumped KSP skin node, placed so the material is destroyed in the flux class where the literature says it stops being used; not a measured decomposition temperature |
| costPerKg | 1.2 | funds/kg | CALIBRATION | - | gameplay cost |
| unlockCost | 800 | funds | CALIBRATION | - | gameplay cost |
| ablation.onsetTemp | 650 | K | CALIBRATION | `model` | skin temperature at which the lumped pyrolysis law switches on |
| ablation.activationTemp | 9500 | K | CALIBRATION | `model` | Arrhenius temperature of the lumped areal rate law (stock uses 7500 K) |
| ablation.rateCoefficient | 28 | kg/(m2 s) | CALIBRATION | `model` | pre-exponential areal rate of the lumped law |
| ablation.heatOfAblation | 8000000 | J/kg | CALIBRATION | `model` | incremental sink per kilogram actually removed in the KSP energy balance. NOT the TPSX 'effective heat of ablation', which includes reradiation and a backface criterion (stock generic ablator: 2.4 MJ/kg) |
| techNode | survivability | tech node id | CALIBRATION | - | progression design: unlock node in the stock tech tree |
| techNode@CommunityTechTree | enhancedSurvivability | tech node id | CALIBRATION | - | progression design: node used when that tech tree is installed (hand-written TechTree/<needs>.cfg) |

### `avcoat-5026-39`: Avcoat Honeycomb

rigid_ablative, FlightProven.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 529 | kg/m3 | SOURCED | `tpsx_avcoat` | 529 kg/m3 (honeycomb-filled HC/G variant, not the moulded block) |
| designThickness | 0.06 | m | CALIBRATION | `model` | reference layer thickness; sized in the mission model for the material's intended missions on stock shields |
| dryArealDensity | 2 | kg/m2 | ASSUMPTION | - | non-consumable bond, carrier, coating or blanket mass per area; not measured |
| specificHeat | 1610 | J/(kg K) | SOURCED | `tpsx_avcoat` | 1610 J/(kg K) |
| emissivity | 0.67 | - | SOURCED | `tpsx_avcoat` | 0.667, rounded to 0.67 |
| charEmissivity | 0.88 | - | ASSUMPTION | - | charred surfaces are treated as darker than the virgin surface; no formulation-specific measurement verified |
| charSurfaceFraction | 0.05 | - | CALIBRATION | `model` | share of the inventory pyrolysed before the surface counts as fully charred |
| maxSurfaceTemp | 2900 | K | CALIBRATION | `model` | native catastrophic fallback. Criterion: gameplay calibration of the lumped KSP skin node, placed so the material is destroyed in the flux class where the literature says it stops being used; not a measured decomposition temperature |
| costPerKg | 5 | funds/kg | CALIBRATION | - | gameplay cost |
| costPerArea | 40 | funds/m2 | CALIBRATION | - | gameplay cost |
| unlockCost | 2500 | funds | CALIBRATION | - | gameplay cost |
| ablation.onsetTemp | 600 | K | CALIBRATION | `model` | skin temperature at which the lumped pyrolysis law switches on |
| ablation.activationTemp | 11000 | K | CALIBRATION | `model` | Arrhenius temperature of the lumped areal rate law (stock uses 7500 K) |
| ablation.rateCoefficient | 30 | kg/(m2 s) | CALIBRATION | `model` | pre-exponential areal rate of the lumped law |
| ablation.heatOfAblation | 12000000 | J/kg | CALIBRATION | `model` | incremental sink per kilogram actually removed in the KSP energy balance. NOT the TPSX 'effective heat of ablation', which includes reradiation and a backface criterion (stock generic ablator: 2.4 MJ/kg) |
| erosion.shearLimit | 90000 | Pa | CALIBRATION | `model` | dynamic pressure above which char or fabric is removed mechanically; ordered by reported pressure capability |
| erosion.shearRate | 0.05 | kg/(m2 s) | CALIBRATION | `model` | areal loss rate at twice the shear limit |
| techNode | landing | tech node id | CALIBRATION | - | progression design: unlock node in the stock tech tree |

### `sla-561v`: SLA-561V

rigid_ablative, FlightProven.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 264 | kg/m3 | SOURCED | `tpsx_sla561v` | 264 kg/m3 |
| designThickness | 0.06 | m | CALIBRATION | `model` | reference layer thickness; sized in the mission model for the material's intended missions on stock shields |
| dryArealDensity | 1.5 | kg/m2 | ASSUMPTION | - | non-consumable bond, carrier, coating or blanket mass per area; not measured |
| specificHeat | 1160 | J/(kg K) | SOURCED | `tpsx_sla561v` | 1160 J/(kg K) |
| emissivity | 0.7 | - | SOURCED | `tpsx_sla561v` | 0.7 |
| charEmissivity | 0.9 | - | ASSUMPTION | - | charred surfaces are treated as darker than the virgin surface; no formulation-specific measurement verified |
| charSurfaceFraction | 0.05 | - | CALIBRATION | `model` | share of the inventory pyrolysed before the surface counts as fully charred |
| maxSurfaceTemp | 2100 | K | CALIBRATION | `model` | native catastrophic fallback. Criterion: gameplay calibration of the lumped KSP skin node, placed so the material is destroyed in the flux class where the literature says it stops being used; not a measured decomposition temperature |
| costPerKg | 7 | funds/kg | CALIBRATION | - | gameplay cost |
| costPerArea | 40 | funds/m2 | CALIBRATION | - | gameplay cost |
| unlockCost | 3500 | funds | CALIBRATION | - | gameplay cost |
| ablation.onsetTemp | 520 | K | CALIBRATION | `model` | skin temperature at which the lumped pyrolysis law switches on |
| ablation.activationTemp | 8500 | K | CALIBRATION | `model` | Arrhenius temperature of the lumped areal rate law (stock uses 7500 K) |
| ablation.rateCoefficient | 14 | kg/(m2 s) | CALIBRATION | `model` | pre-exponential areal rate of the lumped law |
| ablation.heatOfAblation | 9000000 | J/kg | CALIBRATION | `model` | incremental sink per kilogram actually removed in the KSP energy balance. NOT the TPSX 'effective heat of ablation', which includes reradiation and a backface criterion (stock generic ablator: 2.4 MJ/kg) |
| erosion.shearLimit | 20000 | Pa | CALIBRATION | `tpsx_sla561v` | set below the Pathfinder prediction point (about 1.36 MW/m2 at 25 kPa) so that the material is a low-pressure specialist; that point is a prediction, not a test limit |
| erosion.shearRate | 0.12 | kg/(m2 s) | CALIBRATION | `model` | areal loss rate at twice the shear limit |
| techNode | advLanding | tech node id | CALIBRATION | - | progression design: unlock node in the stock tech tree |

### `carbon-phenolic`: Carbon-Phenolic

rigid_ablative, FlightProven.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 1450 | kg/m3 | SOURCED | `nasa_carbon_phenolic` | Galileo FM5055G virgin density 1448 kg/m3, rounded to 1450 |
| designThickness | 0.045 | m | CALIBRATION | `model` | reference layer thickness; sized in the mission model for the material's intended missions on stock shields |
| specificHeat | 1250 | J/(kg K) | ASSUMPTION | - | not verified |
| emissivity | 0.85 | - | ASSUMPTION | - | not verified |
| charEmissivity | 0.9 | - | ASSUMPTION | - | charred surfaces are treated as darker than the virgin surface; no formulation-specific measurement verified |
| charSurfaceFraction | 0.05 | - | CALIBRATION | `model` | share of the inventory pyrolysed before the surface counts as fully charred |
| maxSurfaceTemp | 3600 | K | CALIBRATION | `model` | native catastrophic fallback. Criterion: gameplay calibration of the lumped KSP skin node, placed so the material is destroyed in the flux class where the literature says it stops being used; not a measured decomposition temperature |
| costPerKg | 9 | funds/kg | CALIBRATION | - | gameplay cost |
| unlockCost | 6000 | funds | CALIBRATION | - | gameplay cost |
| ablation.onsetTemp | 800 | K | CALIBRATION | `model` | skin temperature at which the lumped pyrolysis law switches on |
| ablation.activationTemp | 20000 | K | CALIBRATION | `model` | Arrhenius temperature of the lumped areal rate law (stock uses 7500 K) |
| ablation.rateCoefficient | 900 | kg/(m2 s) | CALIBRATION | `model` | pre-exponential areal rate of the lumped law |
| ablation.heatOfAblation | 26000000 | J/kg | CALIBRATION | `model` | incremental sink per kilogram actually removed in the KSP energy balance. NOT the TPSX 'effective heat of ablation', which includes reradiation and a backface criterion (stock generic ablator: 2.4 MJ/kg) |
| techNode | heavyLanding | tech node id | CALIBRATION | - | progression design: unlock node in the stock tech tree |

### `sirca-15`: SIRCA-15

rigid_ablative, FlightProven.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 240 | kg/m3 | SOURCED | `tpsx_sirca` | 240 +/- 3.2 kg/m3 (SIRCA-15F) |
| designThickness | 0.04 | m | CALIBRATION | `model` | reference layer thickness; sized in the mission model for the material's intended missions on stock shields |
| specificHeat | 1120 | J/(kg K) | SOURCED | `tpsx_sirca` | 1120 J/(kg K) |
| emissivity | 0.92 | - | SOURCED | `tpsx_sirca` | 0.921 +/- 0.046, rounded to 0.92 |
| charEmissivity | 0.92 | - | SOURCED | `tpsx_sirca` | kept equal to the virgin value: no separate char value is published |
| charSurfaceFraction | 0.05 | - | CALIBRATION | `model` | share of the inventory pyrolysed before the surface counts as fully charred |
| maxSurfaceTemp | 2000 | K | CALIBRATION | `model` | native catastrophic fallback. Criterion: gameplay calibration of the lumped KSP skin node, placed so the material is destroyed in the flux class where the literature says it stops being used; not a measured decomposition temperature |
| costPerKg | 12 | funds/kg | CALIBRATION | - | gameplay cost |
| unlockCost | 5000 | funds | CALIBRATION | - | gameplay cost |
| ablation.onsetTemp | 700 | K | CALIBRATION | `model` | skin temperature at which the lumped pyrolysis law switches on |
| ablation.activationTemp | 9000 | K | CALIBRATION | `model` | Arrhenius temperature of the lumped areal rate law (stock uses 7500 K) |
| ablation.rateCoefficient | 10 | kg/(m2 s) | CALIBRATION | `model` | pre-exponential areal rate of the lumped law |
| ablation.heatOfAblation | 10000000 | J/kg | CALIBRATION | `model` | incremental sink per kilogram actually removed in the KSP energy balance. NOT the TPSX 'effective heat of ablation', which includes reradiation and a backface criterion (stock generic ablator: 2.4 MJ/kg) |
| erosion.shearLimit | 35000 | Pa | CALIBRATION | `model` | dynamic pressure above which char or fabric is removed mechanically; ordered by reported pressure capability |
| erosion.shearRate | 0.08 | kg/(m2 s) | CALIBRATION | `model` | areal loss rate at twice the shear limit |
| techNode | composites | tech node id | CALIBRATION | - | progression design: unlock node in the stock tech tree |

### `pica`: PICA

rigid_ablative, FlightProven.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 236 | kg/m3 | SOURCED | `tpsx_pica` | 236 +/- 12 kg/m3 (TPSX specimen); another NASA source gives 260-280 for the standard formulation |
| designThickness | 0.07 | m | CALIBRATION | `model` | reference layer thickness; sized in the mission model for the material's intended missions on stock shields |
| dryArealDensity | 1 | kg/m2 | ASSUMPTION | - | non-consumable bond, carrier, coating or blanket mass per area; not measured |
| specificHeat | 1200 | J/(kg K) | SOURCED | `tpsx_pica` | 1200 J/(kg K) |
| emissivity | 0.8 | - | SOURCED | `tpsx_pica` | 0.8 |
| charEmissivity | 0.9 | - | ASSUMPTION | - | charred surfaces are treated as darker than the virgin surface; no formulation-specific measurement verified |
| charSurfaceFraction | 0.05 | - | CALIBRATION | `model` | share of the inventory pyrolysed before the surface counts as fully charred |
| maxSurfaceTemp | 3300 | K | CALIBRATION | `model` | native catastrophic fallback. Criterion: gameplay calibration of the lumped KSP skin node, placed so the material is destroyed in the flux class where the literature says it stops being used; not a measured decomposition temperature |
| costPerKg | 16 | funds/kg | CALIBRATION | - | gameplay cost |
| costPerArea | 60 | funds/m2 | CALIBRATION | - | gameplay cost |
| unlockCost | 12000 | funds | CALIBRATION | - | gameplay cost |
| ablation.onsetTemp | 750 | K | CALIBRATION | `model` | skin temperature at which the lumped pyrolysis law switches on |
| ablation.activationTemp | 16000 | K | CALIBRATION | `model` | Arrhenius temperature of the lumped areal rate law (stock uses 7500 K) |
| ablation.rateCoefficient | 260 | kg/(m2 s) | CALIBRATION | `model` | pre-exponential areal rate of the lumped law |
| ablation.heatOfAblation | 20000000 | J/kg | CALIBRATION | `model` | incremental sink per kilogram actually removed in the KSP energy balance. NOT the TPSX 'effective heat of ablation', which includes reradiation and a backface criterion (stock generic ablator: 2.4 MJ/kg) |
| erosion.shearLimit | 130000 | Pa | CALIBRATION | `model` | dynamic pressure above which char or fabric is removed mechanically; ordered by reported pressure capability |
| erosion.shearRate | 0.06 | kg/(m2 s) | CALIBRATION | `model` | areal loss rate at twice the shear limit |
| techNode | metaMaterials | tech node id | CALIBRATION | - | progression design: unlock node in the stock tech tree |

### `heeet`: HEEET

rigid_ablative, Experimental.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 820 | kg/m3 | ASSUMPTION | `nasa_heeet` | area-averaged value assumed for the dual-layer weave; NASA publishes no layer densities in the cited page |
| designThickness | 0.04 | m | CALIBRATION | `model` | reference layer thickness; sized in the mission model for the material's intended missions on stock shields |
| dryArealDensity | 1.5 | kg/m2 | ASSUMPTION | - | non-consumable bond, carrier, coating or blanket mass per area; not measured |
| specificHeat | 1150 | J/(kg K) | ASSUMPTION | - | not verified |
| emissivity | 0.85 | - | ASSUMPTION | - | not verified |
| charEmissivity | 0.9 | - | ASSUMPTION | - | charred surfaces are treated as darker than the virgin surface; no formulation-specific measurement verified |
| charSurfaceFraction | 0.05 | - | CALIBRATION | `model` | share of the inventory pyrolysed before the surface counts as fully charred |
| maxSurfaceTemp | 3600 | K | CALIBRATION | `model` | native catastrophic fallback. Criterion: gameplay calibration of the lumped KSP skin node, placed so the material is destroyed in the flux class where the literature says it stops being used; not a measured decomposition temperature |
| costPerKg | 30 | funds/kg | CALIBRATION | - | gameplay cost |
| costPerArea | 150 | funds/m2 | CALIBRATION | - | gameplay cost |
| unlockCost | 25000 | funds | CALIBRATION | - | gameplay cost |
| ablation.onsetTemp | 800 | K | CALIBRATION | `model` | skin temperature at which the lumped pyrolysis law switches on |
| ablation.activationTemp | 20000 | K | CALIBRATION | `model` | Arrhenius temperature of the lumped areal rate law (stock uses 7500 K) |
| ablation.rateCoefficient | 800 | kg/(m2 s) | CALIBRATION | `model` | pre-exponential areal rate of the lumped law |
| ablation.heatOfAblation | 30000000 | J/kg | CALIBRATION | `model` | incremental sink per kilogram actually removed in the KSP energy balance. NOT the TPSX 'effective heat of ablation', which includes reradiation and a backface criterion (stock generic ablator: 2.4 MJ/kg) |
| erosion.shearLimit | 500000 | Pa | CALIBRATION | `nasa_heeet` | placed at the order of the reported arc-jet stagnation pressure (about 507 kPa at 50 MW/m2). A stagnation pressure is neither a free-stream dynamic pressure nor a wall shear, so this is a calibration, not a derivation |
| erosion.shearRate | 0.03 | kg/(m2 s) | CALIBRATION | `model` | areal loss rate at twice the shear limit |
| techNode | aerospaceTech | tech node id | CALIBRATION | - | progression design: unlock node in the stock tech tree |
| techNode@CommunityTechTree | aerospaceComposites | tech node id | CALIBRATION | - | progression design: node used when that tech tree is installed (hand-written TechTree/<needs>.cfg) |

### `intumescent-composite`: Intumescent Composite (fictional)

rigid_ablative, Fictional.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 900 | kg/m3 | FICTIONAL | `iop_starlite` | fictional material; Starlite's composition and properties were never published |
| designThickness | 0.016 | m | FICTIONAL | - | fictional thin coating |
| specificHeat | 1400 | J/(kg K) | FICTIONAL | - | fictional |
| emissivity | 0.75 | - | FICTIONAL | - | fictional |
| charEmissivity | 0.95 | - | FICTIONAL | - | fictional |
| charSurfaceFraction | 0.02 | - | FICTIONAL | - | fictional: chars almost immediately |
| maxSurfaceTemp | 2600 | K | FICTIONAL | `model` | fictional; tuned in the mission model |
| costPerKg | 45 | funds/kg | CALIBRATION | - | gameplay cost |
| unlockCost | 30000 | funds | CALIBRATION | - | gameplay cost |
| ablation.onsetTemp | 450 | K | FICTIONAL | `model` | fictional; tuned |
| ablation.activationTemp | 7000 | K | FICTIONAL | `model` | fictional; tuned |
| ablation.rateCoefficient | 2.2 | kg/(m2 s) | FICTIONAL | `model` | fictional; tuned |
| ablation.heatOfAblation | 22000000 | J/kg | FICTIONAL | `model` | fictional; tuned |
| erosion.shearLimit | 40000 | Pa | FICTIONAL | `model` | fictional: the foam is mechanically weak |
| erosion.shearRate | 0.1 | kg/(m2 s) | FICTIONAL | `model` | fictional |
| techNode | experimentalScience | tech node id | CALIBRATION | - | progression design: unlock node in the stock tech tree |
| techNode@CommunityTechTree | advAerospaceEngineering | tech node id | CALIBRATION | - | progression design: node used when that tech tree is installed (hand-written TechTree/<needs>.cfg) |

### `silica-tile-li900`: LI-900 Silica Tile

rigid_reusable, FlightProven.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 144 | kg/m3 | SOURCED | `tpsx_li900` | 144 +/- 2.1 kg/m3 |
| designThickness | 0.06 | m | ASSUMPTION | - | tile thickness assumed; it sets no inventory (the material is not consumed) |
| dryArealDensity | 11 | kg/m2 | ASSUMPTION | - | non-consumable bond, carrier, coating or blanket mass per area; not measured |
| specificHeat | 628 | J/(kg K) | SOURCED | `tpsx_li900` | 628 +/- 84 J/(kg K) |
| emissivity | 0.88 | - | SOURCED | `tpsx_li900` | 0.88 +/- 0.044 |
| charEmissivity | 0.88 | - | SOURCED | `tpsx_li900` | no char: equal to the emissivity |
| maxSurfaceTemp | 1760 | K | SOURCED | `tpsx_li900` | native catastrophic fallback. Criterion: predicted single-use limit about 1760 K (the database also lists melting near 1978 K) |
| reuseSurfaceTemp | 1590 | K | SOURCED | `tpsx_li900` | predicted multiple-use limit about 1590 K; RealTPS only reports when it is exceeded |
| costPerArea | 900 | funds/m2 | CALIBRATION | - | gameplay cost |
| unlockCost | 9000 | funds | CALIBRATION | - | gameplay cost |
| techNode | hypersonicFlight | tech node id | CALIBRATION | - | progression design: unlock node in the stock tech tree |

### `tufroc`: TUFROC

rigid_reusable, FlightProven.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 400 | kg/m3 | ASSUMPTION | - | assumed average of a carbon cap over silica base insulation; not verified against a primary source |
| designThickness | 0.06 | m | ASSUMPTION | - | assumed; it sets no inventory |
| dryArealDensity | 26 | kg/m2 | ASSUMPTION | - | non-consumable bond, carrier, coating or blanket mass per area; not measured |
| specificHeat | 1000 | J/(kg K) | ASSUMPTION | - | not verified |
| emissivity | 0.9 | - | ASSUMPTION | - | not verified |
| charEmissivity | 0.9 | - | ASSUMPTION | - | no char: equal to the emissivity |
| maxSurfaceTemp | 1970 | K | ASSUMPTION | - | native catastrophic fallback. Criterion: about 1970 K is the commonly quoted TUFROC capability; not verified against a primary source in this project |
| costPerArea | 2200 | funds/m2 | CALIBRATION | - | gameplay cost |
| unlockCost | 22000 | funds | CALIBRATION | - | gameplay cost |
| techNode | aerospaceTech | tech node id | CALIBRATION | - | progression design: unlock node in the stock tech tree |

### `reinforced-carbon-carbon`: Reinforced Carbon-Carbon

rigid_reusable, FlightProven.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 1580 | kg/m3 | SOURCED | `tpsx_rcc` | 1580 kg/m3 |
| designThickness | 0.0008 | m | ASSUMPTION | - | thickness of the consumable oxidation coating only; the carbon-carbon structure is dry mass |
| dryArealDensity | 10 | kg/m2 | ASSUMPTION | - | non-consumable bond, carrier, coating or blanket mass per area; not measured |
| specificHeat | 712 | J/(kg K) | SOURCED | `tpsx_rcc` | 712 J/(kg K) |
| emissivity | 0.78 | - | SOURCED | `tpsx_rcc` | 0.78 |
| charEmissivity | 0.78 | - | SOURCED | `tpsx_rcc` | no char: equal to the emissivity |
| maxSurfaceTemp | 2030 | K | SOURCED | `tpsx_rcc` | native catastrophic fallback. Criterion: single-use temperature 2030 K |
| reuseSurfaceTemp | 1920 | K | SOURCED | `tpsx_rcc` | multiple-use temperature 1920 K |
| costPerKg | 400 | funds/kg | CALIBRATION | - | gameplay cost |
| costPerArea | 6000 | funds/m2 | CALIBRATION | - | gameplay cost |
| unlockCost | 28000 | funds | CALIBRATION | - | gameplay cost |
| erosion.oxidationOnsetTemp | 1920 | K | DERIVED | `tpsx_rcc` | the coating is assumed to start losing mass at the reuse temperature |
| erosion.oxidationActivationTemp | 22000 | K | CALIBRATION | - | Arrhenius temperature of the lumped oxidation law |
| erosion.oxidationRate | 60 | kg/(m2 s) | CALIBRATION | - | pre-exponential areal oxidation rate |
| techNode | aerospaceTech | tech node id | CALIBRATION | - | progression design: unlock node in the stock tech tree |
| techNode@CommunityTechTree | aerospaceComposites | tech node id | CALIBRATION | - | progression design: node used when that tech tree is installed (hand-written TechTree/<needs>.cfg) |

### `uhtc-zrb2-sic`: UHTC (ZrB2-SiC)

rigid_reusable, Experimental.

| Property | Value | Unit | Status | Source | Note |
|---|---|---|---|---|---|
| density | 5600 | kg/m3 | ASSUMPTION | - | assumed for a ZrB2-SiC composite; not verified |
| designThickness | 0.0006 | m | ASSUMPTION | - | thickness of the consumable oxide scale only |
| dryArealDensity | 28 | kg/m2 | ASSUMPTION | - | non-consumable bond, carrier, coating or blanket mass per area; not measured |
| specificHeat | 600 | J/(kg K) | ASSUMPTION | - | not verified |
| emissivity | 0.8 | - | ASSUMPTION | - | not verified |
| charEmissivity | 0.8 | - | ASSUMPTION | - | no char: equal to the emissivity |
| maxSurfaceTemp | 2600 | K | CALIBRATION | `nasa_uhtc` | native catastrophic fallback. Criterion: above the roughly 2073 K surface temperature reported for a 3.5 MW/m2, 600 s test; no host exists yet to calibrate against |
| costPerKg | 900 | funds/kg | CALIBRATION | - | gameplay cost |
| costPerArea | 14000 | funds/m2 | CALIBRATION | - | gameplay cost |
| unlockCost | 40000 | funds | CALIBRATION | - | gameplay cost |
| erosion.oxidationOnsetTemp | 2100 | K | ASSUMPTION | - | temperature above which the wear layer oxidises in an oxygen atmosphere |
| erosion.oxidationActivationTemp | 26000 | K | CALIBRATION | - | Arrhenius temperature of the lumped oxidation law |
| erosion.oxidationRate | 400 | kg/(m2 s) | CALIBRATION | - | pre-exponential areal oxidation rate |
| techNode | experimentalScience | tech node id | CALIBRATION | - | progression design: unlock node in the stock tech tree |
| techNode@CommunityTechTree | advAerospaceEngineering | tech node id | CALIBRATION | - | progression design: node used when that tech tree is installed (hand-written TechTree/<needs>.cfg) |
