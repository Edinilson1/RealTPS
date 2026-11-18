# How it works

[← Wiki home](Home.md)

## What RealTPS changes, and what it leaves alone

The game still does the heating: the hot air around the craft, the heat a part radiates away, the heat that flows
between parts, and each part's temperature. RealTPS doesn't add any heat of its own. What it changes is what the
heat shield is made of and what happens to it:

- the material decides how hot the surface can get, how much heat reaches the part behind it, how heavy and
  expensive the shield is, and how it fails;
- ablators burn away to carry the heat off, and once that material is gone, it's gone;
- the face of the shield wears unevenly, cell by cell;
- a shield can be breached, and it can fail.

## Three kinds of protection

| Kind | How it protects | What runs out |
|---|---|---|
| **Ablators** (cork, Avcoat, PICA, carbon-phenolic...) | The material chars and burns away, taking the heat with it | The material. A cell with nothing left is exposed |
| **Reusable surfaces** (tiles, TUFROC, RCC, UHTC) | Nothing burns: the surface glows and radiates the heat away, and insulates what's behind it | Nothing, as long as the surface stays below its limits. Above them the surface deteriorates or the part is destroyed. RCC and UHTC lose a thin coating in oxygen atmospheres when they get very hot |
| **Flexible blankets and fabric** (inflatable shield) | Light insulating layers on a very large, low-density decelerator | Blankets: nothing, they fail by temperature. ADEPT fabric: it slowly erodes and oxidises |

## Cells

The protected face of each shield is split into cells (25 on the stock shields: a centre disc and three rings of
eight). Each cell keeps track of its own material, char, integrity and temperatures.

- The heat doesn't fall evenly: the centre and the side facing into the flow get more. Fly with the shield tilted
  and the windward side wears out first.
- An **engine jet** pointed at the shield (a stage firing at it, a lander's engine, a test stand) wears the spot it
  hits. RealTPS traces each nozzle to find that spot; the heat itself is the game's own exhaust heating.
- A cell that runs out of material is **exposed**: it doesn't protect anymore, and part of the heat that falls on
  it gets through to the part behind the shield (for a pod, into the pod itself).

## Integrity, exposure and structure

Three different things can go wrong, and the shield window shows each of them:

- **Integrity** is damage to what's still protecting a cell: the surface of a tile softening above its limit, or
  the bond under the material staying too hot for too long. Under strong heating an ablator usually burns through
  before its bond gets hot, so its integrity stays at 100% while it's being used up. Integrity drops on tiles, on
  thin layers, and after the heat pulse, when the heat stored in the shield soaks back into the bond.
- **Exposure** is where the material is gone.
- **Structure** is the bare part under the exposed cells. When it gets hotter than it can take, it starts to give
  way.

## Shield failure

Once the structure behind the exposed cells has given way, the shield **fails**. That only happens once:

- the shield stays on the vessel and loses its protection;
- its surface limit drops to that of the bare part (1100 K for the stock rigid shields and pods, 700 K for the
  inflatable shield);
- from then on the game's own heating decides. During the heat pulse the bare part soon goes past that limit and
  the game destroys it, exposing whatever is behind it. After the entry, a shield that has already cooled down
  just stays there.

A shield never comes off by itself.

## Oxygen

RCC, UHTC and the ADEPT fabric oxidise when they're very hot, but only in an atmosphere with oxygen (Kerbin and
Laythe, or Earth in Real Solar System). RealTPS uses the same flag the game uses for jet engines.

## Planet packs and Real Solar System

RealTPS reads the heating the game works out, so a bigger system's longer, hotter entries reach the shield on
their own. There's no special setting for them. Real Solar System also changes the game's heating constants, so
the results there aren't the same as on stock Kerbin: test your missions.

## Difficulty

Each save has its own RealTPS settings, in the game's difficulty options: **Friendly**, **Realistic**, **Strict**
or **Custom**. They only change the damage model: how fast surfaces, bonds and structures give way, how much heat
gets through breaches, a small fixed difference between cells, and switches for damage, failure, breaches and
on-screen warnings. Custom lets you set each one yourself.

| Preset | Damage rate | Heat through breaches | Difference between cells |
|---|---|---|---|
| Friendly | half | a quarter | none |
| Realistic | the calibrated model | half | 2% |
| Strict | 1.5 times | three quarters | 5% |

No preset changes how the materials burn or insulate. A larger solar system, like Real Solar System, doesn't need a
preset of its own: its longer, hotter entries already reach the shield through the game.

## Speculative materials

Two of the materials aren't real: the fictional intumescent composite and the theoretical Gen 3 blanket. If you
don't want them, create an empty folder called `GameData/RealTPSNoSpeculative`.
