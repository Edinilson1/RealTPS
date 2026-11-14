# The shield window

[← Wiki home](Home.md)

Open it with the RealTPS toolbar button, or with **Shield diagnostics** in a shield's part menu. The arrows at the
top switch between the shields on the vessel.

## Cards at the top

| Card | What it shows |
|---|---|
| **Mean integrity** | How sound the protection is, averaged over the face (see [How it works](How-it-works.md)) |
| **Material left** | How much material is left, as a share and as mass (ablators); "not consumed" for reusable surfaces |
| **Face exposed** | How much of the face has no protection left, and how many cells |
| **Structure** | The bare part under the exposed cells: "shield holding" while it's sound, "FAILED – protection lost" once the shield has failed |

## The map

The disc is the protected face seen from the outside, as if you were looking straight at it. Sector 1 is at the
top and the cell numbers go up counter-clockwise. The map is smoothed while the shield is healthy; **Grid** draws
every cell with its number instead. Click a cell to see its numbers on the right, or hover over it for a tooltip.

Each tab shows one thing, and only marks the alerts about that thing:

| Tab | What it shows | Marks |
|---|---|---|
| **Reserve** | How much material is left in each cell | Grey with diagonal stripes: exposed (nothing left). Horizontal lines: less than 10% left |
| **Integrity** | How sound each cell's protection is | White dots: integrity below 20% |
| **Bond temp.** | The estimated temperature of the bond under each cell (under an exposed cell, the bare structure). The scale ends at the bond limit | Vertical lines: bond within 100 K of its limit |
| **Bond margin** | How far each cell's bond is from its limit | Same as Bond temp. |
| **Heat received** | The heat each cell has taken since launch, per square metre, from the air and from engine jets | None |

A marked cell is drawn flat, with a sharp edge, so it stands out from the smoothed map.

## Flight numbers

| Line | What it shows |
|---|---|
| **Skin temperature** | The part's surface temperature and the limit at which the game destroys it (the bar fills up towards that limit) |
| **Internal temperature** | The inside of the part, and its limit |
| **Dynamic pressure** | How hard the air is pushing; some materials erode above a limit |
| **Surface speed** | Speed relative to the air, which is what heats the shield |
| **Face tilt** | The angle between the face and the flow |
| **Cooling / breach heat** | Cooling: the heat the material carries away as it burns. Breach heat: the heat getting through exposed cells to the part behind (or into a pod) |

## The selected cell and the alerts

The cell panel shows the selected cell's reserve, integrity, bond temperature (with its margin) and heat received.
The alert panel counts the cells that are exposed, critical (integrity below 20%), low on material and with the
bond at risk. It also shows the smallest bond margin among the protected cells and names the worst cell for the
tab you're on.

At the bottom you can see whether the data is **live**, or **frozen** while nothing is changing (on rails, or at
high time warp), and which difficulty is in use.
