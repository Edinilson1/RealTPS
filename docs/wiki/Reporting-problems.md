# Reporting problems

[← Wiki home](Home.md)

I can only support the latest RealTPS release on KSP 1.12.5, so please update before reporting. Use the issue
forms on GitHub (bug, balance / realism, mod support, material, feature).

## Before reporting

- **Clean install:** delete any older `GameData/KerbalAblativeMaterials` or `GameData/KerbalThermalProtectionSystems`
  folder, then copy `GameData/RealTPS` from the latest release (or install with CKAN).
- **Known conflict:** Deadly Reentry. Don't use both until there's a compatibility patch.
- **The startup check:** at the main menu, RealTPS checks its parts and materials. If something is wrong (another
  mod changed a shield in a way RealTPS can't handle, or a material's resource is missing), a RealTPS message pops
  up with the problems (up to eight, plus how many more there are). Every one of them is also in `KSP.log`. Please
  mention them in your report.
- **Fewer mods:** try to reproduce the problem with as few mods as you can, ideally just RealTPS, ModuleManager
  and B9PartSwitch.

## The logs

- `KSP.log`, in the KSP install folder, next to the game executable. RealTPS's lines start with `[RealTPS]`.
- `Player.log`, the Unity log, which helps after a crash. On Windows it's in
  `%USERPROFILE%\AppData\LocalLow\Squad\Kerbal Space Program\`.

## Telemetry

For heating, wear or failure problems, turn the telemetry on before the flight. Create a file anywhere in
`GameData` (outside the RealTPS folder, so an update doesn't delete it), for example
`GameData/RealTPS_Telemetry.cfg`, saved as UTF-8 **without** a byte-order mark:

```
RTPS_SETTINGS
{
    telemetryLog = true
}
```

Restart the game. While a shield is hotter than 400 K or burning material, `KSP.log` gets one line per second for
each shield, starting with `[RealTPS] telemetry:`. The fields that matter most:

| Field | Meaning |
|---|---|
| `skinT` / `skinMax` | Surface temperature and the limit at which the game destroys the part, K |
| `inv` | Material left, kg |
| `sinkKw` | Cooling: the heat the burning material carries away, kW |
| `breachKw` / `breachReqKw` | Heat that got through exposed cells to the part behind / heat the exposed cells tried to pass on, kW. If it was requested but not passed, no part was found behind the hole |
| `plumeKw`, `plumeHits` | Heat from engine jets on the shield, kW, and how many nozzles hit it |
| `exposed`, `integrity`, `structure` | Share of the face exposed, mean integrity and structure, 0 to 1 |
| `bondMaxT` | The hottest bond (or bare structure, under an exposed cell), K |
| `deploy` | Inflatable or deployable shields only: how far the shield is open, 0 to 1; `none` if its animation wasn't found |

To turn it off, delete the file or set `telemetryLog = false`.
