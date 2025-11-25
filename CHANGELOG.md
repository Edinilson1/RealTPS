# Changelog

All notable changes to RealTPS. Versions follow semantic versioning. Names of files, config nodes and prefixes are
written as they are today.

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
