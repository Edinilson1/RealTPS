# RealTPS

***Made for every Kerbonaut who was ever toasted in the atmosphere.***

### Welcome to RealTPS

RealTPS (Real Thermal Protection Systems) is a Kerbal Space Program mod that reworks how heat shields work, going
for more realism. Instead of the one generic "Ablator" you get a catalogue of real thermal protection materials,
and a heat shield is no longer just a bar that drains.

## Current version: 0.8.0

**License**: [MIT](LICENSE)\
**KSP version**: 1.12.x\
**Requires**: [ModuleManager], [B9PartSwitch]\
**Optional**: Making History (adds the 1.875 m shield and the KV pods)

## Download and installation

1. Back up your saves.
2. Copy `GameData/RealTPS` from the zip into your KSP `GameData` folder.
3. Make sure ModuleManager and B9PartSwitch are installed. Without them RealTPS does nothing, and a message tells
   you why.

## Materials

**Ablators**: they burn away and take the heat with them.

| Material | What it is | Best for |
|---|---|---|
| Cork-Phenolic | Ground cork in resin, available from the start | Suborbital hops and low orbit |
| Phenolic-Fiberglass | The heat shield of the first capsules | Low orbit, steep Mun returns |
| Avcoat Honeycomb | The Apollo and Orion ablator | Mun returns, interplanetary returns of moderate mass |
| SLA-561V | A very light Mars-lander ablator | Gentle entries and thin atmospheres |
| Carbon-Phenolic | A dense carbon composite for the harshest entries | Anything, if you can carry the mass |
| SIRCA-15 | Light ceramic tiles, small shields only | Light probes coming back from orbit |
| PICA | A light carbon ablator | Light craft on fast returns |
| HEEET | Woven carbon made for giant-planet entries | Heavy capsules coming back from far away |

**Reusable surfaces**: nothing burns; they glow and radiate the heat away.

| Material | What it is | Best for |
|---|---|---|
| LI-900 Silica Tile | The Space Shuttle's tiles | Very gentle entries of very light craft |
| TUFROC | A toughened carbon cap over insulation | Reusable low-orbit returns of light craft on large shields |

**Inflatable heat shield**: light protection spread over a huge area.

| Material | What it is | Best for |
|---|---|---|
| Ceramic-Cloth Blanket (Gen 1) | The basic blanket, available from the start | Light payloads from low orbit |
| Silicon-Carbide Blanket (Gen 2) | A blanket that takes more heat | Low orbit with more margin |

## License

RealTPS is released under the [MIT license](LICENSE). It doesn't contain code, models or configs from any other
mod.

[ModuleManager]: https://ksp.sarbian.com/jenkins/job/ModuleManager/lastStableBuild/
[B9PartSwitch]: https://github.com/KSPModStewards/B9PartSwitch
