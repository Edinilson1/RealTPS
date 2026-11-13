# Placeholder parts

[← Wiki home](Home.md)

> **Some materials sit on stock parts only so they aren't left unusable.** In real life they need a part that
> RealTPS doesn't have yet, with a different shape, a different look and a different construction. Until those
> parts exist, the stock heat shields stand in for them. They work in the thermal model, but the part you see
> isn't what that material would really be fitted to.
>
> **A note from me:** I'm not a 3D modeller. I'm doing my best to learn how to make these shields, and new parts
> will come when I can make them properly. Until then, this is how it stays.

At first, a material with no part that fit it was simply left off the stock parts. I changed that so every material
can be used. A material that no part lists is still allowed (it's loaded, but nobody can pick it, and `validate`
warns about it), but none of the shipped ones is like that anymore.

In the game, the description of each of these materials ends with a **Placeholder part** note.

## Every material, checked against its stock part

| Material | Stock parts | Fit | Why |
|---|---|---|---|
| Cork-phenolic | rigid shields, KV pods | good | Generic early ablator; any blunt shield |
| Phenolic-fibreglass | rigid shields, KV pods | good | Mercury-type blunt heat shield |
| Avcoat 5026-39 | rigid shields, KV pods | good | Apollo and Orion blunt capsule shields |
| SLA-561V | rigid shields | good | Mars aeroshells (blunt sphere-cones); a flat stock shield is a fair stand-in |
| Carbon-phenolic | rigid shields | good | Probe forebodies (sphere-cones); fair stand-in |
| SIRCA-15 | 0.625 m and 1.25 m shields | good | Small probes; already limited to the small shields |
| PICA | rigid shields | good | Stardust, MSL, Dragon-type forebody shields |
| C-PICA | rigid shields, KV pods | good | Conformal forebody shield (Varda) |
| HEEET | rigid shields from 1.25 m | good | Tiled forebody of entry probes |
| Intumescent composite | rigid shields | good | Fictional; no real part to match |
| Gen 1, Gen 2, Gen 3 blankets | inflatable shield | good | The stock inflatable is a HIAD |
| **LI-900 silica tile** | rigid shields | **placeholder** | Real tiles line the belly of a winged vehicle: white and black tiles with gaps, not a round brown shield |
| **TUFROC** | rigid shields | **placeholder** | Made for wing leading edges and nose caps (from general knowledge; I haven't checked it against a source yet) |
| **RCC** | rigid shields (all five) | **placeholder** | A grey hot structure shaped as a nose cap or wing leading edge, with insulation behind it |
| **UHTC (ZrB2-SiC)** | 0.625 m and 1.25 m shields | **placeholder** | A small, sharp ceramic edge or nose tip |
| **ADEPT carbon fabric** | inflatable shield | **placeholder** | Carbon cloth on deployable ribs (like an umbrella), not an inflated stack |

LI-900 and TUFROC were already on the rigid shields (tiles laid out on a disc was an assumption from the start), so
their data didn't change, only their descriptions. RCC, UHTC and the ADEPT fabric had no part before; now they sit
on the stock parts.

## What each placeholder needs from its own part

This is the spec for the parts I still need to make. When one exists, it replaces the stock stand-in (see the last
section).

### RCC (reinforced carbon-carbon)

- **Stand-in now:** HeatShield0, HeatShield1, HeatShield1p5, HeatShield2, HeatShield3 (`rigid_disc`).
- **Real part:** the nose cap or wing leading edge of a spaceplane. Grey, glossy coated carbon. The RCC is the
  structure itself, with insulation behind it, inside the part.
- **Part to make:** host class `hot_structure_panel`, capabilities `hot_structure` and `internal_insulation`. A
  nose cap about 1.25 m across, or a leading-edge segment that fits the stock wings. The insulation behind the
  panel is the part's `backConductance` (low) and, if needed, a `structureLimitTemp` for the frame.
- **On the stand-in:** there's nothing insulating behind it, so the plate under it reaches about 1600 K in low
  orbit (below its own 2030 K limit). Light vehicles on large shields get through low-orbit entries; dense capsules
  and Mun returns fail by surface temperature. Same niche as TUFROC, a bit hotter.

### UHTC (ZrB2-SiC ultra-high-temperature ceramic)

- **Stand-in now:** HeatShield0 and HeatShield1 only (`rigid_disc`). Large ceramic plates wouldn't be believable:
  the material is dense and brittle.
- **Real part:** the sharp nose tip or leading edge of a hypersonic vehicle. Small.
- **Part to make:** host class `hot_structure_panel`, capabilities `hot_structure` and `sharp_leading_edge`. A
  nose tip for 0.625 m, or a thin edge strip.
- **On the stand-in:** survives low orbit even on the heavy 1.25 m capsule (about 2220 K) and fails on Mun
  returns, where the surface would go past its 2600 K limit. Heavy (28 kg/m² fixed) and expensive.

### ADEPT carbon fabric

- **Stand-in now:** InflatableHeatShield (`inflatable_blanket`).
- **Real part:** a deployable umbrella: black carbon cloth stretched over ribs that open like an umbrella. The
  cloth radiates from both sides, and only the ribs touch it.
- **Part to make:** host class `deployable_rib_fabric`, capabilities `rib_supported_fabric` and
  `two_sided_radiation`; an opening animation (`deployAnimation`) and its open radius (`deployedRadius`);
  `structureLimitTemp = 900` for the ribs and joints (an assumption, not checked).
- **What changed for the stand-in:** the cloth's `bondLimitTemp` went from 900 K to 2400 K. The cloth is free
  between the ribs, so nothing is bonded behind it; the limit is the surface fallback, as for RCC and UHTC. The
  900 K was the limit of the ribs and joints, which belongs to the part, not to the cloth. With 900 K the fabric
  failed on every entry on the stand-in.
- **On the stand-in:** it takes Mun returns that the blankets can't (4 t craft, steep Mun return: 2312 K, 24 % of
  the cloth eroded). The 12 t craft reaches 2397 K on the long Mun return, right at the edge of its 2400 K limit.
  The ribs aren't modelled. Picking it makes the part about 60 kg lighter (232 kg less fixed mass, 173 kg of
  cloth).

### LI-900 silica tile

- **Stand-in now:** all rigid shields.
- **Real part:** a tiled belly panel for spaceplanes (Shuttle-type black tiles), or tiles on Mk2/Mk3 fuselage
  parts.
- **Part to make:** a flat or gently curved tile panel. The data doesn't need to change (class `rigid_disc` is
  enough, or a new class if the part is very different).

### TUFROC

- **Stand-in now:** all rigid shields.
- **Real part:** a wing leading edge or nose cap, like RCC.
- **Part to make:** the same parts as RCC can carry it.

## When a dedicated part exists

1. Send the model and textures (Unity 2019.4 with PartTools), attach nodes, mass, size, which axis is the protected
   face, which direction is cell 1, the animation name and open radius (ADEPT), and the tech node and cost.
2. An `RTPS_HOST` for it goes in `data/hosts/`, with the class and capabilities listed above.
3. The material gets its `requiredHostCapabilities` back (listed above).
4. Whether the stock stand-in keeps the material is something to decide then. Taking it off a stock part breaks
   saved craft that use it on that part, so after a public release it's safer to keep it.
5. Run `RtpsTool generate`, `validate` and the tests, and update the simulator rows in
   `docs/sources/MISSION_TEST_MATRIX.md`.
