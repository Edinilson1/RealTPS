# Materials

[← Wiki home](Home.md)

What each material is good for, where it fails, where it unlocks and which parts carry it. The exact numbers
(temperature limits, thickness, conductivity, mass) are in each option's tooltip in the editor, and where they
come from is in `docs/sources/MATERIAL_SOURCES.md`.

When this page says a material "survives" or "fails", that comes from the mod's numerical model of stock Kerbin
entries, unless it says it was a real flight. The unlock nodes are the stock tech tree's; Community Tech Tree moves
some of them (shown in brackets). Materials marked **placeholder** sit on stock parts until they get parts of
their own ([Placeholder parts](Placeholder-parts.md)).

## Ablators

### Cork-Phenolic

Ground cork in phenolic resin. Cheap and light. **Available from the start.** Stock heat shields and Making History
pods.

- **Good for:** suborbital hops and returns from low orbit. On a capsule it uses about a third to a half of its
  layer.
- **Fails:** anything faster than a low-orbit return. On a Mun return the surface overheats within a minute.
- **My own flights (Real Solar System):** on the 2.5 m shield it survived a shallow return from low Earth orbit
  using about 80% of its layer, and a Moon return using 94%, with almost nothing to spare. On the KV-3 pod it was
  destroyed during a Moon return.

### Phenolic-Fiberglass

Glass cloth and phenolic resin, the heat shield of the first capsules. Heavy, and it lets a lot of heat through.
**Survivability** (Enhanced Survivability). Stock heat shields and Making History pods.

- **Good for:** low orbit, and steep returns from the Mun.
- **Fails:** a long, shallow Mun return on a heavy capsule (the thin layer that's left lets its bond overheat, and
  then it burns through), and very fast entries. Light craft on a large shield cope better.

### Avcoat Honeycomb

The Apollo and Orion ablator: resin filled into a honeycomb bonded to the shield. **Landing.** Stock heat shields
and Making History pods.

- **Good for:** it's the lunar-return standard. Low orbit barely touches it, Mun returns use about a third to two
  thirds, and interplanetary returns work with some margin for a moderate mass.
- **Watch out:** a heavy 2.5 m capsule at interplanetary speed uses about 80%. On the Making History pods the
  coating is thinner, and it runs out on Mun returns.

### SLA-561V

A very light silicone-cork ablator, the heat shield of the Viking, Pathfinder and Spirit/Opportunity Mars landers.
**Advanced Landing.** Stock heat shields.

- **Good for:** gentle entries, especially into thin atmospheres (Duna, Mars), and light probes on a large shield.
- **Fails:** steep or heavy entries, because it erodes once dynamic pressure climbs. On a capsule, a low-orbit
  return uses over half of it (almost all of it on a heavy 2.5 m capsule) and a Mun return fails.

### Carbon-Phenolic

Dense carbon cloth and phenolic resin, used on the probes that went into Jupiter and Venus. **Heavy Landing.**
Stock heat shields.

- **Good for:** everything. In the model it survives every return, even the fastest, usually using only a few
  percent to about a fifth of its layer (the fastest return uses all of it on the small 0.625 m probe).
- **Watch out:** it's the heaviest option, and it conducts heat into whatever sits behind it. It's overkill for a
  low-orbit return.

### SIRCA-15

A silicone-impregnated ceramic, machined into small tiles. Extremely light and a very good insulator.
**Composites.** The 0.625 m and 1.25 m shields only.

- **Good for:** light probes coming back from orbit.
- **Fails:** Mun returns and anything faster.

### PICA

A light carbon ablator (Stardust, Curiosity; Dragon flies a variant of it). A quarter of the density of
carbon-phenolic, with nearly the same temperature capability. **Meta-Materials.** Stock heat shields.

- **Good for:** light craft on fast returns. A light probe on a 2.5 m shield survives returns faster than
  interplanetary ones with a good part of its layer left.
- **Fails:** heavy capsules on long entries. The long Mun return uses all of it, and an interplanetary return
  uses all of it or burns through. Even after the pulse, the heat soaking back can still overheat its bond.

### Conformal PICA (C-PICA)

PICA on a flexible carbon felt (Varda capsules). It follows curved surfaces, insulates better and is lighter than
PICA, but it holds less material. **Aerospace Tech** (Aerospace Composites). Stock heat shields and Making History
pods.

- **Good for:** the light option for the Making History pods (low orbit, steep Mun returns), and light craft in
  general.
- **Fails:** it runs out sooner than PICA on long or very fast entries.

### HEEET

A carbon ablator woven in three dimensions, with an insulating layer, made for entries into Venus and the giant
planets. **Aerospace Tech** (Aerospace Composites). Stock heat shields of 1.25 m and up.

- **Good for:** heavy capsules coming back from far away. In the model it survives every return using about a
  third or less, and lets little heat through.

### Intumescent Composite (fictional)

A thin coating that swells into an insulating foam when heated, inspired by the "Starlite"-type claims. It's not a
real material (and the swelling itself isn't simulated). **Experimental Science** (Advanced Aerospace
Engineering). Stock heat shields.

- **Good for:** feather-light protection for low orbit and light craft.
- **Watch out:** it's fragile under dynamic pressure, and a heavy capsule on a Mun return uses most of it.

## Reusable surfaces

Nothing burns here: they survive by radiating the heat away. Their niche is light vehicles with a lot of shield
area for their mass, on gentle entries.

### LI-900 Silica Tile (placeholder)

The white and black tiles of the Space Shuttle. A superb insulator with a low temperature limit. **Hypersonic
Flight.** Stock heat shields.

- **Good for:** suborbital hops and very gentle entries of very light craft.
- **Fails:** above about 1640 K the tiles soften and fail within seconds. In the model even light craft fail on
  orbital returns.

### TUFROC (placeholder)

A toughened carbon cap over silica insulation, made for wing leading edges. **Aerospace Tech.** Stock heat shields.

- **Good for:** light vehicles on large shields coming back from low orbit, again and again.
- **Fails:** dense capsules and Mun returns.

### Reinforced Carbon-Carbon (placeholder)

The nose cap and wing leading edges of the Space Shuttle: a hot structure that radiates the heat away.
**Aerospace Tech** (Aerospace Composites). Stock heat shields.

- **Good for:** the same niche as TUFROC, with a slightly higher limit. It's reusable as long as it stays below
  its reuse limit; above that, its coating slowly oxidises in an oxygen atmosphere.
- **Fails:** dense capsules and Mun returns. On a stock shield there's nothing insulating behind it, so the part
  behind it gets hot.

### UHTC (ZrB2-SiC) (placeholder)

An ultra-high-temperature ceramic for sharp leading edges. Very dense, brittle and expensive. **Experimental
Science** (Advanced Aerospace Engineering). The 0.625 m and 1.25 m shields only.

- **Good for:** it's the reusable option that takes the most heat, and it survives low-orbit returns even on a
  heavy 1.25 m capsule.
- **Fails:** Mun returns. At the highest temperatures it loses its oxide scale in an oxygen atmosphere.

## Inflatable shield

The stock 10 m inflatable heat shield spreads a light craft over a huge area, so the heating per square metre is
low.

### Ceramic-Cloth Blanket (Gen 1)

The basic flexible protection of an inflatable decelerator. **Available from the start.**

- **Good for:** light payloads from low orbit.
- **Fails:** Mun returns.

### Silicon-Carbide Blanket (Gen 2)

Takes noticeably more heat than Gen 1 and keeps the inflatable structure cooler. **Meta-Materials.**

- **Good for:** low orbit with more margin.
- **Fails:** Mun returns.

### Advanced Aerogel Blanket (Gen 3, theoretical)

A projected blanket that has never flown, and the most capable of the three. **Experimental Science** (Aerospace
Composites).

- **Good for:** low orbit with the most margin of the three.
- **Fails:** Mun returns. In my Real Solar System test, the inflatable was destroyed on a Moon return.

### Woven Carbon Fabric (ADEPT) (placeholder)

Carbon cloth stretched over deployable ribs, carried here by the inflatable shield. **Aerospace Tech** (Aerospace
Composites).

- **Good for:** faster entries than the blankets can take. In the model a 4 t craft survives Mun returns, eroding
  about a quarter of the cloth. It doesn't oxidise on Duna or Eve.
- **Fails:** interplanetary returns. A 12 t craft gets through the long Mun return right at the edge of its limit,
  and fails the steep one.
