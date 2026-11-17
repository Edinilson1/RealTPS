# Mission test matrix (numerical model)

**These are not in-game results.** They come from `tools/RtpsTool simulate`, a planar point-mass entry with the
stock thermal formulas (re-implemented from `Physics.cfg` and the decompiled `FlightIntegrator`) around the
RealTPS cell solver, the conduction mapping, the breach ledger and the failure policy. The model has one skin node,
like the game, but its convective area, its payload and its soak are simplifications. It ranks materials and
sizes parameters; only real flights in the game prove anything.

Made with `RtpsTool simulate` (face-on) and `RtpsTool simulate --tilt 12`. Regenerate the tables after any change
to a material, a host or the solver.

## How to read a cell

`ok 1704K 38% b480 x12% g0.40 i350` = the craft came through; peak skin temperature; share of the inventory
used; peak bond (substrate) temperature under a protected cell; share of the face exposed at the end; lowest cell
integrity; peak internal temperature. `FAIL overtemp` = the skin passed the material's native fallback while
some cell still protected; `FAIL burnthrough` = nothing protected any more (all cells exposed, or the shield
failed) and the skin passed the bare carrier's limit; `SOAK@Ns` = the craft came through and the shield failed
N s after the entry. A failed shield stays on the vessel. `skip` = the craft skipped out of the atmosphere.

The simulator uses no cell-to-cell variation, so on a face-on entry all cells of one band fail together. In the
game the Realistic preset adds a fixed 2 % variation. These tables are Realistic without that
variation; `RtpsTool simulate --preset NAME` runs any preset, variation included.

## What changed with the cell model

- The derived conduction (from `conductivity` and the remaining thickness) gives results within a few kelvin of
  the hand-calibrated multipliers it replaced; the material niches carry over.
- Ablators now end by **running out** or by the **bond overheating**, and then burn through at the carrier limit
  (1100 K on the stock shields) like the pre-cell model did after depletion. PICA and phenolic-fibreglass on the
  heavy 1.25 m capsule use their whole layer on the long, shallow Mun return (482 MJ/m²) and on fast returns:
  some still come through because the last cells go after the heat pulse (`ok … 100% x100%`), others burn
  through. They survive the steep Mun return (182 MJ/m²) with margin. Avcoat and carbon-phenolic, heavier per
  area, survive everything their descriptions promise.
- With the face tilted 12°, the leeward cells keep material longer and several marginal cases change outcome.
  The result of a marginal entry depends on attitude, as intended.
- Silica tiles and TUFROC fail by surface temperature on dense capsules, before and after: their niche is light
  vehicles on large shields. Their surface deterioration (`g` < 1) now starts well before the fallback.
- The KV pods (thinner integral coating) run out on the Mun return and burn through.
- C-PICA is PICA with a lower conductivity and a 50 mm layer instead of 70 mm: about 11 %
  lighter per area, with a cooler bond wherever both survive, and about 12 % less material, so it runs out sooner
  than PICA on long and fast entries (6 t capsule, Mun return Pe35: 89 % used and 18 % exposed, PICA 77 %). On
  the KV pods it is the light option: less than half of Avcoat's mass, fine in low orbit and on the steep Mun
  return, out of material sooner than Avcoat on the long one.
- RCC, UHTC and the ADEPT fabric are placeholders on stock parts
  (`docs/wiki/Placeholder-parts.md`). RCC sits in the TUFROC niche with a slightly higher limit: light
  vehicles on large shields come through low-orbit entries, dense capsules and Mun returns do not; nothing
  insulates the stock shield behind it, so its bond (substrate) reaches 1600 K, under its own 2030 K limit. UHTC,
  on the two small shields only, survives low orbit even on the heavy 1.25 m capsule (2222 K) and fails on Mun
  returns (its radiative equilibrium passes 2600 K). The ADEPT fabric on the inflatable takes Mun returns that the
  blankets cannot (4 t: Pe15 at 2312 K, 24 % eroded); the 12 t craft reaches 2397 K, at the edge of its 2400 K
  limit, on the long return.

## Face-on

Face tilted 0 degrees from the direction of travel; each entry is followed by a 600 s soak.

### 1.25 m capsule (1.0 t) on HeatShield1

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 84 W/cm2, 23 MJ/m2, 60 kPa, 6.2 g | 117 W/cm2, 116 MJ/m2, 32 kPa, 3.4 g | 129 W/cm2, 102 MJ/m2, 35 kPa, 3.7 g | 307 W/cm2, 482 MJ/m2, 26 kPa, 3.1 g | 317 W/cm2, 182 MJ/m2, 53 kPa, 5.6 g | 413 W/cm2, 420 MJ/m2, 34 kPa, 3.6 g | 524 W/cm2, 429 MJ/m2, 60 kPa, 6.4 g | 650 W/cm2, 429 MJ/m2, 111 kPa, 11.8 g |
| stock generic (reference) | ok 1794K 4% i312 | ok 1950K 20% i347 | ok 2002K 18% i336 | ok 2567K 86% i437 | ok 2593K 32% i331 | ok 2820K 74% i382 | skip 3049K 73% i361 | skip 3270K 71% i351 |
| cork-phenolic | ok 1599K 9% b300 i301 | ok 1711K 40% b304 i303 | ok 1752K 35% b302 i302 | FAIL overtemp @42s 13% b300 | FAIL overtemp @29s 8% b300 | FAIL overtemp @9s 2% b300 | FAIL overtemp @4s 1% b300 | FAIL overtemp @1s 0% b300 |
| phenolic-fiberglass | ok 1580K 3% b311 i304 | ok 1673K 18% b354 i317 | ok 1709K 16% b340 i313 | FAIL burnthrough @318s 100% b560 x100% g0.00 | ok 2099K 34% b341 i312 | ok 2231K 100% b556 x100% g0.00 i336 | FAIL overtemp @6s 2% b300 | FAIL overtemp @3s 1% b300 |
| avcoat-5026-39 | ok 1664K 3% b303 i302 | ok 1749K 15% b316 i309 | ok 1784K 14% b312 i307 | ok 2148K 70% b430 i331 | ok 2169K 32% b312 i306 | ok 2297K 69% b388 i321 | skip 2442K 79% b390 i321 | skip 2568K 81% b357 i317 |
| sla-561v | ok 1560K 31% b301 i301 | ok 1662K 58% b307 i304 | ok 1699K 56% b305 i303 | FAIL overtemp @75s 57% b300 | FAIL overtemp @41s 24% b300 | FAIL overtemp @16s 11% b300 | FAIL overtemp @5s 3% b300 | FAIL overtemp @2s 1% b300 |
| carbon-phenolic | ok 1779K 0% b318 i305 | ok 1867K 2% b379 i321 | ok 1895K 2% b360 i316 | ok 2150K 14% b476 g0.98 i343 | ok 2165K 7% b345 i312 | ok 2244K 15% b413 i327 | skip 2318K 18% b359 i321 | skip 2384K 19% b339 i317 |
| sirca-15 | ok 1649K 15% b302 i301 | ok 1764K 57% b320 i307 | ok 1805K 51% b314 i305 | FAIL overtemp @46s 22% b300 | FAIL overtemp @31s 14% b300 | FAIL overtemp @9s 3% b300 | FAIL overtemp @4s 2% b300 | FAIL overtemp @2s 1% b300 |
| silica-tile-li900 | FAIL overtemp @59s b300 g0.80 | FAIL overtemp @176s b301 g0.31 | FAIL overtemp @103s b300 g0.49 | FAIL overtemp @15s b300 g0.93 | FAIL overtemp @11s b300 g0.96 | FAIL overtemp @5s b300 g0.98 | FAIL overtemp @2s b300 g0.99 | FAIL overtemp @1s b300 |
| pica | ok 1694K 2% b307 i303 | ok 1770K 15% b335 i313 | ok 1798K 14% b326 i310 | ok 2071K 100% b563 x100% g0.00 i345 | ok 2087K 38% b327 i309 | ok 2174K 100% b569 x100% g0.00 i333 | FAIL burnthrough @134s 100% b623 x100% g0.00 | FAIL burnthrough @104s 100% b637 x100% g0.00 |
| c-pica | ok 1693K 3% b304 i302 | ok 1770K 17% b322 i309 | ok 1798K 16% b316 i307 | ok 2071K 100% b574 x100% g0.00 i345 | ok 2087K 43% b318 i307 | ok 2174K 100% b598 x100% g0.00 i336 | FAIL burnthrough @97s 100% b488 x100% | FAIL burnthrough @77s 100% b444 x100% |
| tufroc | ok 1946K b305 g0.51 i303 | FAIL overtemp @206s b304 g0.41 | FAIL overtemp @124s b302 g0.56 | FAIL overtemp @21s b300 g0.79 | FAIL overtemp @16s b300 g0.85 | FAIL overtemp @6s b300 g0.98 | FAIL overtemp @3s b300 g0.99 | FAIL overtemp @1s b300 |
| heeet | ok 1773K 1% b313 i305 | ok 1859K 4% b360 i320 | ok 1887K 4% b345 i315 | ok 2144K 23% b444 g0.99 i339 | ok 2159K 11% b335 i311 | ok 2238K 26% b397 i326 | skip 2312K 31% b352 i322 | skip 2378K 33% b333 i318 |
| intumescent-composite | ok 1551K 6% b304 i302 | ok 1667K 22% b321 i309 | ok 1709K 20% b316 i307 | ok 2182K 92% b551 x51% g0.00 i335 | ok 2213K 39% b317 i307 | ok 2389K 77% b432 i322 | skip 2563K 90% b559 g0.08 i326 | FAIL overtemp @3s 1% b300 |
| reinforced-carbon-carbon | ok 1987K 1% b1256 i321 | FAIL overtemp @207s 1% b1610 | FAIL overtemp @126s 1% b1498 | FAIL overtemp @21s 0% b721 | FAIL overtemp @16s 0% b611 | FAIL overtemp @6s 0% b422 | FAIL overtemp @3s 0% b349 | FAIL overtemp @1s 0% b321 |
| uhtc-zrb2-sic | ok 1982K 0% b1211 i317 | ok 2166K 2% b1849 i367 | ok 2222K 3% b1831 i352 | FAIL overtemp @52s 6% b1452 | FAIL overtemp @34s 3% b1132 | FAIL overtemp @11s 1% b600 | FAIL overtemp @4s 0% b418 | FAIL overtemp @2s 0% b349 |

### 2.5 m capsule (6 t) on HeatShield2

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 102 W/cm2, 27 MJ/m2, 89 kPa, 6.3 g | 143 W/cm2, 138 MJ/m2, 48 kPa, 3.4 g | 157 W/cm2, 121 MJ/m2, 53 kPa, 3.8 g | 314 W/cm2, 375 MJ/m2, 10 kPa, 0.7 g | 319 W/cm2, 202 MJ/m2, 74 kPa, 5.3 g | 414 W/cm2, 444 MJ/m2, 38 kPa, 2.7 g | 525 W/cm2, 435 MJ/m2, 67 kPa, 4.9 g | 650 W/cm2, 435 MJ/m2, 127 kPa, 9.2 g |
| stock generic (reference) | ok 1876K 5% i311 | ok 2051K 24% i343 | ok 2105K 21% i333 | skip 2576K 66% i365 | ok 2589K 35% i329 | skip 2811K 76% i363 | skip 3038K 72% i345 | skip 3259K 70% i338 |
| cork-phenolic | ok 1674K 15% b301 i301 | ok 1804K 51% b304 i303 | ok 1846K 47% b303 i302 | FAIL overtemp @42s 12% b300 | FAIL overtemp @29s 8% b300 | FAIL overtemp @9s 2% b300 | FAIL overtemp @4s 1% b300 | FAIL overtemp @1s 0% b300 |
| phenolic-fiberglass | ok 1644K 4% b312 i304 | ok 1752K 22% b359 i316 | ok 1787K 20% b344 i312 | skip 2094K 70% b528 g0.66 i332 | ok 2102K 38% b348 i311 | FAIL burnthrough @194s 100% b583 x100% g0.00 | FAIL overtemp @6s 2% b300 | FAIL overtemp @3s 1% b300 |
| avcoat-5026-39 | ok 1726K 4% b303 i302 | ok 1827K 20% b318 i308 | ok 1862K 18% b313 i306 | skip 2164K 66% b351 i318 | ok 2172K 36% b314 i306 | skip 2297K 81% b443 i322 | skip 2442K 82% b348 i313 | skip 2568K 83% b334 i311 |
| sla-561v | ok 1630K 60% b301 i301 | ok 1747K 98% b326 x18% i305 | ok 1786K 96% b318 i304 | FAIL overtemp @67s 47% b300 | FAIL overtemp @40s 23% b300 | FAIL overtemp @16s 11% b300 | FAIL overtemp @5s 3% b300 | FAIL overtemp @2s 1% b300 |
| carbon-phenolic | ok 1834K 0% b320 i305 | ok 1927K 3% b384 i320 | ok 1954K 3% b364 i315 | skip 2160K 13% b392 i326 | ok 2167K 7% b350 i312 | skip 2244K 18% b382 i323 | skip 2318K 19% b338 i315 | skip 2384K 20% b326 i313 |
| silica-tile-li900 | FAIL overtemp @58s b300 g0.81 | FAIL overtemp @176s b301 g0.32 | FAIL overtemp @102s b300 g0.50 | FAIL overtemp @15s b300 g0.93 | FAIL overtemp @11s b300 g0.96 | FAIL overtemp @5s b300 g0.98 | FAIL overtemp @2s b300 g0.99 | FAIL overtemp @1s b300 |
| pica | ok 1747K 3% b307 i303 | ok 1833K 20% b337 i312 | ok 1861K 18% b328 i309 | skip 2084K 77% b524 g0.76 i327 | ok 2089K 43% b332 i308 | FAIL burnthrough @159s 100% b618 x100% g0.00 | FAIL burnthrough @105s 100% b604 x100% g0.69 | FAIL burnthrough @80s 100% b484 x100% |
| c-pica | ok 1746K 4% b304 i302 | ok 1833K 22% b323 i308 | ok 1861K 21% b317 i306 | skip 2084K 89% b572 x18% g0.00 i326 | ok 2090K 48% b322 i306 | FAIL burnthrough @123s 100% b516 x100% g0.99 | FAIL burnthrough @85s 100% b432 x100% | FAIL burnthrough @68s 100% b408 x100% |
| tufroc | FAIL overtemp @66s b300 g0.82 | FAIL overtemp @203s b304 g0.47 | FAIL overtemp @123s b302 g0.60 | FAIL overtemp @21s b300 g0.79 | FAIL overtemp @16s b300 g0.85 | FAIL overtemp @6s b300 g0.98 | FAIL overtemp @3s b300 g0.99 | FAIL overtemp @1s b300 |
| heeet | ok 1829K 1% b314 i304 | ok 1919K 5% b363 i318 | ok 1947K 5% b348 i314 | skip 2154K 23% b372 i324 | ok 2161K 13% b339 i311 | skip 2239K 31% b372 i323 | skip 2312K 33% b330 i315 | skip 2378K 34% b320 i312 |
| intumescent-composite | ok 1630K 16% b304 i302 | ok 1765K 28% b324 i308 | ok 1809K 27% b317 i306 | skip 2207K 78% b427 i320 | ok 2218K 56% b324 i306 | skip 2390K 91% b573 x18% g0.00 i325 | skip 2563K 93% b546 g0.80 i317 | FAIL overtemp @3s 1% b300 |
| reinforced-carbon-carbon | FAIL overtemp @67s 0% b1089 | FAIL overtemp @204s 1% b1596 | FAIL overtemp @124s 1% b1485 | FAIL overtemp @21s 0% b720 | FAIL overtemp @16s 0% b610 | FAIL overtemp @6s 0% b422 | FAIL overtemp @3s 0% b349 | FAIL overtemp @1s 0% b321 |

### 0.625 m probe (0.25 t) on HeatShield0

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 75 W/cm2, 20 MJ/m2, 48 kPa, 6.1 g | 103 W/cm2, 104 MJ/m2, 26 kPa, 3.4 g | 114 W/cm2, 91 MJ/m2, 28 kPa, 3.7 g | 290 W/cm2, 357 MJ/m2, 19 kPa, 2.7 g | 311 W/cm2, 169 MJ/m2, 44 kPa, 5.8 g | 408 W/cm2, 340 MJ/m2, 31 kPa, 4.1 g | 519 W/cm2, 428 MJ/m2, 55 kPa, 7.3 g | 644 W/cm2, 10 MJ/m2, 0 kPa, 0.0 g |
| stock generic (reference) | ok 1797K 4% i314 | ok 1952K 21% i356 | ok 2003K 18% i343 | ok 2620K 72% i409 | ok 2675K 34% i338 | ok 2915K 66% i376 | skip 3151K 81% i407 | FAIL overtemp @3s 1% |
| cork-phenolic | ok 1564K 8% b300 i301 | ok 1671K 36% b303 i303 | ok 1710K 32% b302 i302 | FAIL overtemp @43s 13% b300 | FAIL overtemp @30s 8% b300 | FAIL overtemp @9s 2% b300 | FAIL overtemp @4s 1% b300 | FAIL overtemp @1s 0% b300 |
| phenolic-fiberglass | ok 1551K 3% b310 i304 | ok 1639K 16% b351 i318 | ok 1673K 14% b338 i314 | ok 2055K 62% b498 g0.26 i340 | ok 2097K 32% b338 i312 | ok 2231K 63% b453 g0.97 i329 | FAIL overtemp @6s 2% b300 | FAIL overtemp @3s 1% b300 |
| avcoat-5026-39 | ok 1636K 3% b303 i302 | ok 1714K 14% b316 i309 | ok 1747K 13% b312 i307 | ok 2125K 57% b360 i321 | ok 2167K 30% b311 i306 | ok 2296K 59% b347 i316 | ok 2442K 100% b558 x100% g0.00 i348 | skip 2568K 78% b400 i326 |
| sla-561v | ok 1528K 21% b301 i301 | ok 1625K 46% b307 i304 | ok 1660K 43% b305 i303 | FAIL burnthrough @109s 100% b338 x100% | FAIL overtemp @41s 24% b300 | FAIL overtemp @16s 11% b300 | FAIL overtemp @5s 3% b300 | FAIL overtemp @2s 1% b300 |
| carbon-phenolic | ok 1752K 0% b318 i306 | ok 1840K 2% b377 i322 | ok 1867K 2% b358 i317 | ok 2137K 11% b425 i333 | ok 2164K 6% b344 i313 | ok 2244K 13% b386 i323 | ok 2318K 100% b549 x100% g0.00 i364 | skip 2384K 18% b359 i323 |
| sirca-15 | ok 1614K 10% b302 i302 | ok 1724K 51% b318 i308 | ok 1762K 46% b313 i306 | FAIL overtemp @46s 23% b300 | FAIL overtemp @31s 14% b300 | FAIL overtemp @9s 3% b300 | FAIL overtemp @4s 2% b300 | FAIL overtemp @2s 1% b300 |
| silica-tile-li900 | FAIL overtemp @59s b300 g0.78 | FAIL overtemp @177s b301 g0.29 | FAIL overtemp @103s b300 g0.48 | FAIL overtemp @15s b300 g0.93 | FAIL overtemp @11s b300 g0.96 | FAIL overtemp @5s b300 g0.98 | FAIL overtemp @2s b300 g0.99 | FAIL overtemp @1s b300 |
| pica | ok 1668K 2% b307 i304 | ok 1741K 13% b334 i315 | ok 1769K 12% b325 i311 | ok 2054K 64% b443 g0.99 i332 | ok 2085K 35% b325 i310 | ok 2173K 72% b440 i325 | FAIL burnthrough @169s 100% b593 x100% g0.00 | FAIL burnthrough @132s 100% b612 x100% g0.00 |
| c-pica | ok 1668K 2% b304 i303 | ok 1741K 15% b321 i311 | ok 1768K 14% b316 i308 | ok 2054K 73% b429 i327 | ok 2085K 40% b317 i307 | ok 2174K 82% b455 g0.98 i323 | FAIL burnthrough @126s 100% b628 x100% g0.00 | FAIL burnthrough @89s 100% b531 x100% g0.99 |
| tufroc | ok 1899K b304 g0.96 i303 | FAIL overtemp @208s b304 g0.34 | FAIL overtemp @126s b302 g0.53 | FAIL overtemp @21s b300 g0.79 | FAIL overtemp @16s b300 g0.85 | FAIL overtemp @6s b300 g0.98 | FAIL overtemp @3s b300 g0.99 | FAIL overtemp @1s b300 |
| intumescent-composite | ok 1515K 4% b304 i302 | ok 1625K 20% b321 i310 | ok 1665K 18% b315 i308 | ok 2150K 68% b396 i325 | ok 2210K 35% b316 i307 | ok 2388K 66% b367 i318 | FAIL burnthrough @265s 100% b570 x100% g0.00 | FAIL overtemp @3s 1% b300 |
| reinforced-carbon-carbon | ok 1937K 0% b1226 i323 | FAIL overtemp @210s 1% b1622 | FAIL overtemp @127s 1% b1508 | FAIL overtemp @21s 0% b721 | FAIL overtemp @16s 0% b611 | FAIL overtemp @6s 0% b422 | FAIL overtemp @3s 0% b349 | FAIL overtemp @1s 0% b321 |
| uhtc-zrb2-sic | ok 1933K 0% b1183 i319 | ok 2111K 1% b1805 i374 | ok 2165K 2% b1787 i358 | FAIL overtemp @52s 6% b1463 | FAIL overtemp @34s 3% b1136 | FAIL overtemp @11s 1% b600 | FAIL overtemp @4s 0% b418 | FAIL overtemp @2s 0% b349 |

### KV-1 pod with service stack (1.6 t) on kv1Pod

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 76 W/cm2, 21 MJ/m2, 69 kPa, 6.2 g | 109 W/cm2, 109 MJ/m2, 38 kPa, 3.4 g | 120 W/cm2, 95 MJ/m2, 41 kPa, 3.7 g | 276 W/cm2, 346 MJ/m2, 9 kPa, 0.9 g | 284 W/cm2, 167 MJ/m2, 60 kPa, 5.5 g | 299 W/cm2, 24 MJ/m2, 1 kPa, 0.1 g | 323 W/cm2, 7 MJ/m2, 0 kPa, 0.0 g | 339 W/cm2, 5 MJ/m2, 0 kPa, 0.0 g |
| stock generic (reference) | ok 2308K 10% i305 | ok 2540K 54% i320 | ok 2605K 47% i315 | skip 3244K 100% i332 | ok 3266K 66% i313 | FAIL overtemp @17s 9% | FAIL overtemp @5s 2% | FAIL overtemp @2s 1% |
| cork-phenolic | ok 1645K 19% b301 i300 | ok 1769K 71% b314 i302 | ok 1811K 64% b309 i301 | FAIL overtemp @42s 19% b300 | FAIL overtemp @29s 12% b300 | FAIL overtemp @9s 3% b300 | FAIL overtemp @4s 1% b300 | FAIL overtemp @1s 0% b300 |
| phenolic-fiberglass | ok 1616K 6% b325 i302 | ok 1721K 31% b431 i307 | ok 1756K 28% b399 i305 | FAIL burnthrough @154s 100% b604 x100% g0.00 | ok 2101K 56% b434 i304 | FAIL burnthrough @102s 100% b626 x100% g0.00 | FAIL overtemp @6s 3% b300 | FAIL overtemp @3s 2% b300 |
| avcoat-5026-39 | ok 1694K 5% b307 i301 | ok 1797K 27% b339 i304 | ok 1833K 25% b329 i303 | FAIL burnthrough @198s 100% b605 x100% g0.00 | ok 2171K 53% b342 i303 | FAIL burnthrough @114s 100% b599 x100% g0.75 | FAIL burnthrough @82s 100% b514 x100% g0.99 | FAIL burnthrough @66s 100% b483 x100% |
| c-pica | ok 1723K 5% b309 i301 | ok 1810K 31% b351 i303 | ok 1838K 29% b338 i302 | FAIL burnthrough @130s 100% b525 x100% g0.99 | ok 2089K 71% b383 i302 | FAIL burnthrough @75s 100% b446 x100% | FAIL burnthrough @52s 100% b407 x100% | FAIL burnthrough @40s 100% b385 x100% |

### light probe on a 2.5 m shield (1.0 t) on HeatShield2

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 57 W/cm2, 16 MJ/m2, 26 kPa, 5.9 g | 78 W/cm2, 80 MJ/m2, 14 kPa, 3.3 g | 87 W/cm2, 70 MJ/m2, 15 kPa, 3.6 g | 251 W/cm2, 219 MJ/m2, 9 kPa, 2.4 g | 309 W/cm2, 143 MJ/m2, 26 kPa, 6.2 g | 412 W/cm2, 247 MJ/m2, 23 kPa, 5.8 g | 525 W/cm2, 279 MJ/m2, 41 kPa, 10.2 g | 650 W/cm2, 284 MJ/m2, 74 kPa, 18.0 g |
| stock generic (reference) | ok 1630K 3% i309 | ok 1755K 13% i335 | ok 1801K 12% i327 | ok 2408K 39% i342 | ok 2561K 25% i322 | ok 2807K 42% i333 | ok 3037K 47% i334 | ok 3258K 46% i332 |
| cork-phenolic | ok 1444K 4% b300 i300 | ok 1529K 23% b302 i302 | ok 1565K 21% b302 i301 | FAIL overtemp @44s 13% b300 | FAIL overtemp @30s 8% b300 | FAIL overtemp @9s 2% b300 | FAIL overtemp @4s 1% b300 | FAIL overtemp @1s 0% b300 |
| phenolic-fiberglass | ok 1444K 2% b308 i303 | ok 1520K 10% b342 i313 | ok 1552K 9% b331 i310 | ok 1955K 34% b362 i315 | ok 2076K 24% b328 i308 | ok 2229K 43% b357 i312 | FAIL overtemp @6s 2% b300 | FAIL overtemp @3s 1% b300 |
| avcoat-5026-39 | ok 1527K 2% b302 i302 | ok 1590K 9% b312 i306 | ok 1623K 8% b309 i305 | ok 2024K 31% b317 i307 | ok 2144K 23% b308 i304 | ok 2294K 41% b316 i306 | ok 2442K 47% b319 i307 | ok 2568K 47% b318 i306 |
| sla-561v | ok 1412K 5% b301 i301 | ok 1491K 28% b304 i302 | ok 1524K 25% b303 i302 | ok 1958K 87% b350 i306 | ok 2098K 64% b305 i302 | FAIL overtemp @16s 11% b300 | FAIL overtemp @5s 3% b300 | FAIL overtemp @2s 1% b300 |
| carbon-phenolic | ok 1649K 0% b315 i304 | ok 1739K 1% b367 i316 | ok 1768K 1% b351 i313 | ok 2076K 6% b370 i316 | ok 2152K 5% b337 i309 | ok 2243K 9% b352 i312 | ok 2318K 11% b352 i312 | ok 2384K 12% b347 i311 |
| silica-tile-li900 | ok 1721K b301 g0.68 i301 | FAIL overtemp @183s b301 g0.15 | FAIL overtemp @106s b300 g0.40 | FAIL overtemp @15s b300 g0.93 | FAIL overtemp @11s b300 g0.96 | FAIL overtemp @5s b300 g0.98 | FAIL overtemp @2s b300 g0.99 | FAIL overtemp @1s b300 |
| pica | ok 1566K 1% b305 i302 | ok 1634K 7% b327 i309 | ok 1663K 7% b319 i307 | ok 1979K 33% b338 i310 | ok 2067K 26% b317 i305 | ok 2172K 49% b340 i309 | ok 2259K 58% b352 i309 | ok 2335K 59% b351 i309 |
| c-pica | ok 1565K 1% b303 i302 | ok 1632K 8% b316 i307 | ok 1661K 8% b312 i305 | ok 1979K 38% b325 i308 | ok 2067K 29% b311 i304 | ok 2172K 56% b330 i307 | ok 2258K 65% b343 i308 | ok 2335K 67% b344 i307 |
| tufroc | ok 1721K b303 i302 | ok 1872K b317 i308 | ok 1923K b313 g0.45 i306 | FAIL overtemp @21s b300 g0.78 | FAIL overtemp @16s b300 g0.85 | FAIL overtemp @6s b300 g0.98 | FAIL overtemp @3s b300 g0.99 | FAIL overtemp @1s b300 |
| heeet | ok 1637K 0% b310 i304 | ok 1725K 2% b349 i314 | ok 1755K 2% b337 i311 | ok 2063K 10% b351 i314 | ok 2143K 8% b327 i308 | ok 2237K 16% b340 i311 | ok 2312K 19% b341 i310 | ok 2378K 19% b337 i310 |
| intumescent-composite | ok 1383K 3% b303 i302 | ok 1473K 13% b315 i306 | ok 1510K 12% b311 i305 | ok 2010K 37% b324 i307 | ok 2173K 26% b310 i304 | ok 2385K 45% b322 i306 | ok 2562K 48% b324 i307 | FAIL overtemp @3s 1% b300 |
| reinforced-carbon-carbon | ok 1744K 0% b1107 i314 | ok 1904K 0% b1645 i355 | ok 1958K 1% b1634 i342 | FAIL overtemp @21s 0% b724 | FAIL overtemp @16s 0% b613 | FAIL overtemp @6s 0% b422 | FAIL overtemp @3s 0% b349 | FAIL overtemp @1s 0% b321 |

### light payload on a 3.75 m shield (2.5 t) on HeatShield3

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 59 W/cm2, 16 MJ/m2, 28 kPa, 5.9 g | 80 W/cm2, 82 MJ/m2, 15 kPa, 3.3 g | 89 W/cm2, 72 MJ/m2, 16 kPa, 3.6 g | 255 W/cm2, 227 MJ/m2, 10 kPa, 2.4 g | 310 W/cm2, 146 MJ/m2, 27 kPa, 6.1 g | 413 W/cm2, 253 MJ/m2, 24 kPa, 5.6 g | 525 W/cm2, 287 MJ/m2, 42 kPa, 9.9 g | 651 W/cm2, 292 MJ/m2, 76 kPa, 17.6 g |
| stock generic (reference) | ok 1634K 3% i309 | ok 1760K 14% i337 | ok 1806K 12% i328 | ok 2409K 40% i345 | ok 2554K 25% i324 | ok 2797K 43% i335 | ok 3026K 47% i337 | ok 3246K 47% i335 |
| cork-phenolic | ok 1452K 5% b300 i300 | ok 1538K 24% b302 i302 | ok 1575K 21% b302 i302 | FAIL overtemp @44s 13% b300 | FAIL overtemp @30s 8% b300 | FAIL overtemp @9s 2% b300 | FAIL overtemp @4s 1% b300 | FAIL overtemp @1s 0% b300 |
| phenolic-fiberglass | ok 1451K 2% b309 i303 | ok 1528K 10% b343 i313 | ok 1560K 9% b332 i310 | ok 1962K 35% b365 i316 | ok 2078K 25% b329 i308 | ok 2229K 44% b360 i313 | FAIL overtemp @6s 2% b300 | FAIL overtemp @3s 1% b300 |
| avcoat-5026-39 | ok 1535K 2% b302 i302 | ok 1598K 9% b313 i307 | ok 1631K 8% b309 i305 | ok 2032K 32% b318 i308 | ok 2147K 23% b308 i304 | ok 2294K 42% b317 i307 | ok 2442K 48% b320 i307 | ok 2568K 49% b319 i307 |
| sla-561v | ok 1420K 6% b301 i301 | ok 1500K 29% b304 i303 | ok 1533K 26% b303 i302 | ok 1967K 90% b379 i308 | FAIL overtemp @46s 30% b300 | FAIL overtemp @16s 11% b300 | FAIL overtemp @5s 3% b300 | FAIL overtemp @2s 1% b300 |
| carbon-phenolic | ok 1657K 0% b315 i304 | ok 1746K 1% b368 i317 | ok 1775K 1% b351 i313 | ok 2081K 6% b372 i318 | ok 2153K 5% b337 i310 | ok 2243K 10% b353 i313 | ok 2318K 11% b354 i313 | ok 2384K 12% b350 i312 |
| silica-tile-li900 | ok 1734K b301 g0.53 i301 | FAIL overtemp @182s b301 g0.17 | FAIL overtemp @106s b300 g0.41 | FAIL overtemp @15s b300 g0.93 | FAIL overtemp @11s b300 g0.96 | FAIL overtemp @5s b300 g0.98 | FAIL overtemp @2s b300 g0.99 | FAIL overtemp @1s b300 |
| pica | ok 1573K 1% b305 i303 | ok 1641K 7% b327 i310 | ok 1670K 7% b320 i308 | ok 1985K 35% b340 i311 | ok 2070K 27% b318 i306 | ok 2172K 50% b343 i310 | ok 2259K 59% b356 i311 | ok 2335K 61% b356 i310 |
| c-pica | ok 1573K 1% b303 i302 | ok 1640K 8% b317 i307 | ok 1668K 8% b312 i305 | ok 1984K 39% b327 i308 | ok 2070K 30% b311 i304 | ok 2172K 57% b332 i308 | ok 2258K 67% b347 i309 | ok 2335K 69% b349 i308 |
| tufroc | ok 1733K b303 i302 | ok 1885K b318 g0.97 i308 | ok 1936K b313 g0.05 i306 | FAIL overtemp @21s b300 g0.78 | FAIL overtemp @16s b300 g0.85 | FAIL overtemp @6s b300 g0.98 | FAIL overtemp @3s b300 g0.99 | FAIL overtemp @1s b300 |
| heeet | ok 1645K 0% b311 i304 | ok 1732K 2% b350 i315 | ok 1762K 2% b337 i312 | ok 2068K 10% b354 i316 | ok 2145K 8% b327 i309 | ok 2237K 16% b341 i312 | ok 2312K 19% b342 i312 | ok 2378K 20% b339 i311 |
| intumescent-composite | ok 1391K 3% b303 i302 | ok 1483K 13% b316 i307 | ok 1521K 12% b311 i305 | ok 2021K 38% b325 i308 | ok 2179K 27% b311 i304 | ok 2385K 46% b323 i307 | ok 2562K 50% b326 i307 | FAIL overtemp @3s 1% b300 |
| reinforced-carbon-carbon | ok 1757K 0% b1116 i315 | ok 1919K 0% b1657 i361 | ok 1972K 2% b1647 i347 | FAIL overtemp @21s 0% b724 | FAIL overtemp @16s 0% b613 | FAIL overtemp @6s 0% b422 | FAIL overtemp @3s 0% b349 | FAIL overtemp @1s 0% b321 |

### 10 m inflatable (12 t) on InflatableHeatShield

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 34 W/cm2, 10 MJ/m2, 9 kPa, 5.4 g | 44 W/cm2, 47 MJ/m2, 5 kPa, 3.0 g | 49 W/cm2, 41 MJ/m2, 5 kPa, 3.3 g | 92 W/cm2, 11 MJ/m2, 0 kPa, 0.3 g | 106 W/cm2, 11 MJ/m2, 1 kPa, 0.4 g | 220 W/cm2, 10 MJ/m2, 0 kPa, 0.3 g | 420 W/cm2, 9 MJ/m2, 0 kPa, 0.2 g | 614 W/cm2, 9 MJ/m2, 0 kPa, 0.1 g |
| ftps-nextel-gen1 | ok 1450K b340 i302 | ok 1725K b435 i309 | ok 1776K b410 i307 | FAIL overtemp @24s b301 | FAIL overtemp @20s b301 | FAIL overtemp @10s b300 | FAIL overtemp @6s b300 | FAIL overtemp @3s b300 |
| ftps-sic-gen2 | ok 1449K b313 i301 | ok 1716K b348 i305 | ok 1767K b339 i304 | FAIL overtemp @28s b300 | FAIL overtemp @23s b300 | FAIL overtemp @11s b300 | FAIL overtemp @6s b300 | FAIL overtemp @3s b300 |
| ftps-aerogel-gen3 | ok 1441K b310 i301 | ok 1705K b337 i304 | ok 1756K b330 i303 | FAIL overtemp @34s b301 | FAIL overtemp @26s b300 | FAIL overtemp @12s b300 | FAIL overtemp @6s b300 | FAIL overtemp @4s b300 |
| adept-carbon-fabric | ok 1416K 0% b1007 i310 | ok 1685K 6% b1510 i336 | ok 1736K 6% b1525 i329 | ok 2397K 58% b2042 i337 | FAIL overtemp @34s 9% b1291 | FAIL overtemp @13s 2% b651 | FAIL overtemp @7s 1% b452 | FAIL overtemp @4s 0% b392 |

### 10 m inflatable (4 t) on InflatableHeatShield

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 23 W/cm2, 7 MJ/m2, 3 kPa, 4.9 g | 28 W/cm2, 30 MJ/m2, 2 kPa, 2.8 g | 32 W/cm2, 26 MJ/m2, 2 kPa, 3.1 g | 89 W/cm2, 11 MJ/m2, 0 kPa, 0.6 g | 103 W/cm2, 11 MJ/m2, 1 kPa, 0.9 g | 218 W/cm2, 10 MJ/m2, 0 kPa, 0.7 g | 419 W/cm2, 9 MJ/m2, 0 kPa, 0.5 g | 613 W/cm2, 9 MJ/m2, 0 kPa, 0.3 g |
| ftps-nextel-gen1 | ok 1186K b337 i302 | ok 1531K b413 i307 | ok 1583K b393 i306 | FAIL overtemp @25s b301 | FAIL overtemp @21s b301 | FAIL overtemp @10s b300 | FAIL overtemp @6s b300 | FAIL overtemp @3s b300 |
| ftps-sic-gen2 | ok 1193K b313 i301 | ok 1528K b341 i304 | ok 1579K b333 i303 | FAIL overtemp @28s b301 | FAIL overtemp @23s b300 | FAIL overtemp @11s b300 | FAIL overtemp @6s b300 | FAIL overtemp @3s b300 |
| ftps-aerogel-gen3 | ok 1180K b310 i301 | ok 1514K b331 i303 | ok 1564K b325 i303 | FAIL overtemp @38s b301 | FAIL overtemp @27s b300 | FAIL overtemp @12s b300 | FAIL overtemp @6s b300 | FAIL overtemp @4s b300 |
| adept-carbon-fabric | ok 1155K 0% b856 i309 | ok 1488K 2% b1338 i330 | ok 1540K 2% b1347 i324 | ok 2201K 27% b1795 i328 | ok 2312K 24% b1738 i322 | FAIL overtemp @13s 2% b657 | FAIL overtemp @7s 1% b452 | FAIL overtemp @4s 0% b392 |

## Face tilted 12 degrees

Face tilted 12 degrees from the direction of travel; each entry is followed by a 600 s soak.

### 1.25 m capsule (1.0 t) on HeatShield1

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 84 W/cm2, 23 MJ/m2, 60 kPa, 6.2 g | 117 W/cm2, 116 MJ/m2, 32 kPa, 3.4 g | 129 W/cm2, 102 MJ/m2, 35 kPa, 3.7 g | 307 W/cm2, 482 MJ/m2, 26 kPa, 3.1 g | 317 W/cm2, 182 MJ/m2, 53 kPa, 5.6 g | 413 W/cm2, 420 MJ/m2, 34 kPa, 3.6 g | 524 W/cm2, 429 MJ/m2, 60 kPa, 6.4 g | 650 W/cm2, 429 MJ/m2, 111 kPa, 11.8 g |
| stock generic (reference) | ok 1794K 4% i312 | ok 1950K 20% i347 | ok 2002K 18% i336 | ok 2567K 86% i437 | ok 2593K 32% i331 | ok 2820K 74% i382 | skip 3049K 73% i361 | skip 3270K 71% i351 |
| cork-phenolic | ok 1599K 9% b300 i301 | ok 1711K 40% b304 i303 | ok 1752K 35% b303 i302 | FAIL overtemp @42s 13% b300 | FAIL overtemp @29s 8% b300 | FAIL overtemp @9s 2% b300 | FAIL overtemp @4s 1% b300 | FAIL overtemp @1s 0% b300 |
| phenolic-fiberglass | ok 1580K 3% b311 i304 | ok 1673K 18% b354 i317 | ok 1709K 16% b340 i313 | ok 2078K 100% b563 x100% g0.00 i361 | ok 2099K 34% b342 i312 | ok 2231K 92% b568 x78% g0.00 i337 | FAIL overtemp @6s 2% b300 | FAIL overtemp @3s 1% b300 |
| avcoat-5026-39 | ok 1664K 3% b303 i302 | ok 1749K 15% b317 i309 | ok 1784K 14% b312 i307 | ok 2148K 70% b457 g0.94 i331 | ok 2169K 32% b312 i306 | ok 2297K 69% b408 i321 | skip 2442K 79% b447 i321 | skip 2568K 81% b403 i317 |
| sla-561v | ok 1560K 31% b301 i301 | ok 1662K 58% b308 i304 | ok 1699K 56% b306 i303 | FAIL overtemp @75s 57% b300 | FAIL overtemp @41s 24% b300 | FAIL overtemp @16s 11% b300 | FAIL overtemp @5s 3% b300 | FAIL overtemp @2s 1% b300 |
| carbon-phenolic | ok 1779K 0% b318 i305 | ok 1867K 2% b379 i321 | ok 1895K 2% b360 i316 | ok 2150K 14% b478 g0.97 i343 | ok 2165K 7% b346 i312 | ok 2244K 15% b415 i327 | skip 2318K 18% b361 i321 | skip 2384K 19% b340 i317 |
| sirca-15 | ok 1649K 15% b302 i301 | ok 1764K 57% b321 i307 | ok 1805K 51% b315 i305 | FAIL overtemp @46s 22% b300 | FAIL overtemp @31s 14% b300 | FAIL overtemp @9s 3% b300 | FAIL overtemp @4s 2% b300 | FAIL overtemp @2s 1% b300 |
| silica-tile-li900 | FAIL overtemp @59s b300 g0.79 | FAIL overtemp @176s b301 g0.26 | FAIL overtemp @103s b300 g0.45 | FAIL overtemp @15s b300 g0.93 | FAIL overtemp @11s b300 g0.95 | FAIL overtemp @5s b300 g0.98 | FAIL overtemp @2s b300 g0.99 | FAIL overtemp @1s b300 |
| pica | ok 1694K 2% b307 i303 | ok 1770K 15% b335 i313 | ok 1798K 14% b326 i310 | ok 2071K 94% b568 x82% g0.00 i347 | ok 2087K 38% b328 i309 | ok 2174K 92% b584 x69% g0.00 i334 | skip 2259K 98% b636 x82% g0.00 i342 | skip 2335K 98% b633 x82% g0.00 i333 |
| c-pica | ok 1693K 3% b304 i302 | ok 1770K 17% b322 i309 | ok 1798K 16% b316 i307 | ok 2071K 96% b580 x82% g0.00 i347 | ok 2087K 43% b319 i307 | ok 2174K 96% b624 x78% g0.00 i337 | FAIL burnthrough @112s 100% b487 x100% | FAIL burnthrough @83s 100% b430 x100% |
| tufroc | ok 1946K b305 g0.48 i303 | FAIL overtemp @206s b304 g0.37 | FAIL overtemp @124s b302 g0.54 | FAIL overtemp @21s b300 g0.77 | FAIL overtemp @16s b300 g0.84 | FAIL overtemp @6s b300 g0.97 | FAIL overtemp @3s b300 g0.99 | FAIL overtemp @1s b300 |
| heeet | ok 1773K 1% b313 i305 | ok 1859K 4% b360 i320 | ok 1887K 4% b345 i315 | ok 2144K 23% b447 g0.98 i339 | ok 2159K 11% b335 i311 | ok 2238K 26% b400 i326 | skip 2312K 31% b355 i322 | skip 2378K 33% b335 i318 |
| intumescent-composite | ok 1551K 6% b304 i302 | ok 1667K 22% b322 i309 | ok 1709K 20% b316 i307 | ok 2182K 90% b573 x51% g0.00 i337 | ok 2213K 39% b318 i307 | ok 2389K 77% b467 g0.94 i322 | skip 2563K 90% b610 x39% g0.00 i327 | FAIL overtemp @3s 1% b300 |
| reinforced-carbon-carbon | ok 1987K 1% b1256 i321 | FAIL overtemp @207s 1% b1610 | FAIL overtemp @126s 1% b1498 | FAIL overtemp @21s 0% b721 | FAIL overtemp @16s 0% b611 | FAIL overtemp @6s 0% b422 | FAIL overtemp @3s 0% b349 | FAIL overtemp @1s 0% b321 |
| uhtc-zrb2-sic | ok 1982K 0% b1211 i317 | ok 2166K 2% b1849 i367 | ok 2222K 3% b1831 i352 | FAIL overtemp @52s 6% b1452 | FAIL overtemp @34s 3% b1132 | FAIL overtemp @11s 1% b600 | FAIL overtemp @4s 0% b418 | FAIL overtemp @2s 0% b349 |

### 2.5 m capsule (6 t) on HeatShield2

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 102 W/cm2, 27 MJ/m2, 89 kPa, 6.3 g | 143 W/cm2, 138 MJ/m2, 48 kPa, 3.4 g | 157 W/cm2, 121 MJ/m2, 53 kPa, 3.8 g | 314 W/cm2, 375 MJ/m2, 10 kPa, 0.7 g | 319 W/cm2, 202 MJ/m2, 74 kPa, 5.3 g | 414 W/cm2, 444 MJ/m2, 38 kPa, 2.7 g | 525 W/cm2, 435 MJ/m2, 67 kPa, 4.9 g | 650 W/cm2, 435 MJ/m2, 127 kPa, 9.2 g |
| stock generic (reference) | ok 1876K 5% i311 | ok 2051K 24% i343 | ok 2105K 21% i333 | skip 2576K 66% i365 | ok 2589K 35% i329 | skip 2811K 76% i363 | skip 3038K 72% i345 | skip 3259K 70% i338 |
| cork-phenolic | ok 1674K 15% b301 i301 | ok 1804K 51% b304 i303 | ok 1846K 47% b303 i302 | FAIL overtemp @42s 12% b300 | FAIL overtemp @29s 8% b300 | FAIL overtemp @9s 2% b300 | FAIL overtemp @4s 1% b300 | FAIL overtemp @1s 0% b300 |
| phenolic-fiberglass | ok 1644K 4% b312 i304 | ok 1752K 22% b360 i316 | ok 1787K 20% b345 i312 | skip 2094K 72% b559 x12% g0.00 i332 | ok 2102K 38% b350 i311 | skip 2232K 95% b593 x82% g0.00 i337 | FAIL overtemp @6s 2% b300 | FAIL overtemp @3s 1% b300 |
| avcoat-5026-39 | ok 1726K 4% b303 i302 | ok 1827K 20% b318 i308 | ok 1862K 18% b313 i306 | skip 2164K 66% b364 i318 | ok 2172K 36% b314 i306 | skip 2297K 81% b536 g0.68 i322 | skip 2442K 82% b390 i314 | skip 2568K 83% b375 i311 |
| sla-561v | ok 1630K 60% b301 i301 | ok 1747K 95% b326 x43% i305 | ok 1786K 94% b322 x37% i304 | FAIL overtemp @67s 47% b300 | FAIL overtemp @40s 23% b300 | FAIL overtemp @16s 11% b300 | FAIL overtemp @5s 3% b300 | FAIL overtemp @2s 1% b300 |
| carbon-phenolic | ok 1834K 0% b320 i305 | ok 1927K 3% b384 i320 | ok 1954K 3% b364 i315 | skip 2160K 13% b393 i326 | ok 2167K 7% b350 i312 | skip 2244K 18% b384 i323 | skip 2318K 19% b339 i315 | skip 2384K 20% b327 i313 |
| silica-tile-li900 | FAIL overtemp @58s b300 g0.80 | FAIL overtemp @176s b301 g0.28 | FAIL overtemp @102s b300 g0.47 | FAIL overtemp @15s b300 g0.93 | FAIL overtemp @11s b300 g0.95 | FAIL overtemp @5s b300 g0.98 | FAIL overtemp @2s b300 g0.99 | FAIL overtemp @1s b300 |
| pica | ok 1747K 3% b307 i303 | ok 1833K 20% b338 i312 | ok 1861K 18% b328 i309 | skip 2084K 80% b566 x20% g0.00 i327 | ok 2089K 43% b334 i308 | skip 2174K 100% b636 x100% g0.00 i341 | skip 2259K 99% b637 x82% g0.00 i324 | FAIL burnthrough @90s 100% b510 x100% |
| c-pica | ok 1746K 4% b304 i302 | ok 1833K 22% b324 i308 | ok 1861K 21% b318 i306 | skip 2084K 90% b592 x43% g0.00 i327 | ok 2090K 48% b324 i306 | FAIL burnthrough @140s 100% b514 x100% g0.99 | FAIL burnthrough @90s 100% b421 x100% | FAIL burnthrough @72s 100% b399 x100% |
| tufroc | FAIL overtemp @66s b300 g0.81 | FAIL overtemp @203s b304 g0.44 | FAIL overtemp @123s b302 g0.58 | FAIL overtemp @21s b300 g0.77 | FAIL overtemp @16s b300 g0.84 | FAIL overtemp @6s b300 g0.97 | FAIL overtemp @3s b300 g0.99 | FAIL overtemp @1s b300 |
| heeet | ok 1829K 1% b314 i304 | ok 1919K 5% b363 i318 | ok 1947K 5% b348 i314 | skip 2154K 23% b374 i324 | ok 2161K 13% b339 i311 | skip 2239K 31% b375 i323 | skip 2312K 33% b331 i315 | skip 2378K 34% b321 i312 |
| intumescent-composite | ok 1630K 16% b304 i302 | ok 1765K 28% b324 i308 | ok 1809K 27% b318 i306 | skip 2207K 78% b474 g0.98 i320 | ok 2218K 56% b328 i307 | skip 2390K 91% b597 x43% g0.00 i326 | skip 2563K 92% b602 x37% g0.60 i317 | FAIL overtemp @3s 1% b300 |
| reinforced-carbon-carbon | FAIL overtemp @67s 0% b1089 | FAIL overtemp @204s 1% b1596 | FAIL overtemp @124s 1% b1485 | FAIL overtemp @21s 0% b720 | FAIL overtemp @16s 0% b610 | FAIL overtemp @6s 0% b422 | FAIL overtemp @3s 0% b349 | FAIL overtemp @1s 0% b321 |

### 0.625 m probe (0.25 t) on HeatShield0

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 75 W/cm2, 20 MJ/m2, 48 kPa, 6.1 g | 103 W/cm2, 104 MJ/m2, 26 kPa, 3.4 g | 114 W/cm2, 91 MJ/m2, 28 kPa, 3.7 g | 290 W/cm2, 357 MJ/m2, 19 kPa, 2.7 g | 311 W/cm2, 169 MJ/m2, 44 kPa, 5.8 g | 408 W/cm2, 340 MJ/m2, 31 kPa, 4.1 g | 519 W/cm2, 428 MJ/m2, 55 kPa, 7.3 g | 644 W/cm2, 10 MJ/m2, 0 kPa, 0.0 g |
| stock generic (reference) | ok 1797K 4% i314 | ok 1952K 21% i356 | ok 2003K 18% i343 | ok 2620K 72% i409 | ok 2675K 34% i338 | ok 2915K 66% i376 | skip 3151K 81% i407 | FAIL overtemp @3s 1% |
| cork-phenolic | ok 1565K 8% b300 i301 | ok 1671K 36% b303 i303 | ok 1710K 32% b302 i302 | FAIL overtemp @43s 13% b300 | FAIL overtemp @30s 8% b300 | FAIL overtemp @9s 2% b300 | FAIL overtemp @4s 1% b300 | FAIL overtemp @1s 0% b300 |
| phenolic-fiberglass | ok 1551K 3% b310 i304 | ok 1639K 16% b352 i318 | ok 1673K 14% b339 i314 | ok 2055K 70% b521 x24% g0.00 i340 | ok 2097K 32% b339 i312 | ok 2231K 63% b473 g0.85 i329 | FAIL overtemp @6s 2% b300 | FAIL overtemp @3s 1% b300 |
| avcoat-5026-39 | ok 1636K 3% b303 i302 | ok 1714K 14% b316 i309 | ok 1747K 13% b312 i307 | ok 2125K 57% b367 i321 | ok 2167K 30% b311 i306 | ok 2296K 59% b354 i316 | ok 2442K 92% b564 x69% g0.00 i350 | skip 2568K 78% b457 i326 |
| sla-561v | ok 1528K 21% b301 i301 | ok 1625K 46% b307 i304 | ok 1660K 43% b305 i303 | FAIL overtemp @108s 97% b334 x69% | FAIL overtemp @41s 24% b300 | FAIL overtemp @16s 11% b300 | FAIL overtemp @5s 3% b300 | FAIL overtemp @2s 1% b300 |
| carbon-phenolic | ok 1752K 0% b318 i306 | ok 1840K 2% b377 i322 | ok 1867K 2% b358 i317 | ok 2137K 11% b426 i333 | ok 2164K 6% b344 i313 | ok 2244K 13% b387 i323 | ok 2318K 100% b553 x100% g0.00 i364 | skip 2384K 18% b360 i323 |
| sirca-15 | ok 1614K 10% b302 i302 | ok 1724K 51% b319 i308 | ok 1762K 46% b313 i306 | FAIL overtemp @46s 23% b300 | FAIL overtemp @31s 14% b300 | FAIL overtemp @9s 3% b300 | FAIL overtemp @4s 2% b300 | FAIL overtemp @2s 1% b300 |
| silica-tile-li900 | FAIL overtemp @59s b300 g0.77 | FAIL overtemp @177s b301 g0.25 | FAIL overtemp @103s b300 g0.45 | FAIL overtemp @15s b300 g0.93 | FAIL overtemp @11s b300 g0.95 | FAIL overtemp @5s b300 g0.98 | FAIL overtemp @2s b300 g0.99 | FAIL overtemp @1s b300 |
| pica | ok 1668K 2% b307 i304 | ok 1741K 13% b334 i315 | ok 1769K 12% b325 i311 | ok 2054K 64% b462 g0.94 i332 | ok 2085K 35% b326 i310 | ok 2173K 72% b469 g0.93 i326 | FAIL detached @230s 100% b609 x100% g0.00 | skip 2335K 99% b633 x94% g0.00 i357 |
| c-pica | ok 1668K 2% b304 i303 | ok 1741K 15% b321 i311 | ok 1768K 14% b316 i308 | ok 2054K 73% b457 g0.97 i328 | ok 2085K 40% b318 i307 | ok 2174K 81% b516 g0.38 i324 | FAIL detached @179s 100% b628 x100% g0.00 | FAIL burnthrough @119s 100% b633 x100% g0.00 |
| tufroc | ok 1899K b304 g0.96 i303 | FAIL overtemp @208s b304 g0.30 | FAIL overtemp @126s b302 g0.50 | FAIL overtemp @21s b300 g0.77 | FAIL overtemp @16s b300 g0.84 | FAIL overtemp @6s b300 g0.97 | FAIL overtemp @3s b300 g0.99 | FAIL overtemp @1s b300 |
| intumescent-composite | ok 1515K 4% b304 i302 | ok 1625K 20% b321 i310 | ok 1665K 18% b315 i308 | ok 2150K 68% b411 i325 | ok 2210K 35% b316 i307 | ok 2388K 66% b377 i318 | ok 2563K 97% b583 x82% g0.00 i359 | FAIL overtemp @3s 1% b300 |
| reinforced-carbon-carbon | ok 1937K 0% b1226 i323 | FAIL overtemp @210s 1% b1622 | FAIL overtemp @127s 1% b1508 | FAIL overtemp @21s 0% b721 | FAIL overtemp @16s 0% b611 | FAIL overtemp @6s 0% b422 | FAIL overtemp @3s 0% b349 | FAIL overtemp @1s 0% b321 |
| uhtc-zrb2-sic | ok 1933K 0% b1183 i319 | ok 2111K 1% b1805 i374 | ok 2165K 2% b1787 i358 | FAIL overtemp @52s 6% b1463 | FAIL overtemp @34s 3% b1136 | FAIL overtemp @11s 1% b600 | FAIL overtemp @4s 0% b418 | FAIL overtemp @2s 0% b349 |

### KV-1 pod with service stack (1.6 t) on kv1Pod

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 76 W/cm2, 21 MJ/m2, 69 kPa, 6.2 g | 109 W/cm2, 109 MJ/m2, 38 kPa, 3.4 g | 120 W/cm2, 95 MJ/m2, 41 kPa, 3.7 g | 276 W/cm2, 346 MJ/m2, 9 kPa, 0.9 g | 284 W/cm2, 167 MJ/m2, 60 kPa, 5.5 g | 299 W/cm2, 24 MJ/m2, 1 kPa, 0.1 g | 323 W/cm2, 7 MJ/m2, 0 kPa, 0.0 g | 339 W/cm2, 5 MJ/m2, 0 kPa, 0.0 g |
| stock generic (reference) | ok 2308K 10% i305 | ok 2540K 54% i320 | ok 2605K 47% i315 | skip 3244K 100% i332 | ok 3266K 66% i313 | FAIL overtemp @17s 9% | FAIL overtemp @5s 2% | FAIL overtemp @2s 1% |
| cork-phenolic | ok 1645K 19% b301 i300 | ok 1769K 71% b318 i302 | ok 1811K 64% b310 i301 | FAIL overtemp @42s 19% b300 | FAIL overtemp @29s 12% b300 | FAIL overtemp @9s 3% b300 | FAIL overtemp @4s 1% b300 | FAIL overtemp @1s 0% b300 |
| phenolic-fiberglass | ok 1616K 6% b325 i302 | ok 1721K 31% b434 i307 | ok 1756K 28% b401 i305 | FAIL burnthrough @170s 100% b610 x100% g0.00 | ok 2101K 56% b444 g0.99 i304 | FAIL overtemp @103s 97% b631 x78% g0.00 | FAIL overtemp @6s 3% b300 | FAIL overtemp @3s 2% b300 |
| avcoat-5026-39 | ok 1694K 5% b307 i301 | ok 1797K 27% b340 i304 | ok 1833K 25% b330 i303 | FAIL burnthrough @248s 100% b616 x100% g0.00 | ok 2171K 53% b346 i303 | FAIL burnthrough @130s 100% b606 x100% g0.69 | FAIL burnthrough @89s 100% b497 x100% | FAIL burnthrough @73s 100% b466 x100% |
| c-pica | ok 1723K 5% b309 i301 | ok 1810K 31% b352 i303 | ok 1838K 29% b339 i302 | FAIL burnthrough @139s 100% b507 x100% | ok 2089K 71% b398 i302 | FAIL burnthrough @79s 100% b433 x100% | FAIL burnthrough @55s 100% b397 x100% | FAIL burnthrough @43s 100% b376 x100% |

### light probe on a 2.5 m shield (1.0 t) on HeatShield2

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 57 W/cm2, 16 MJ/m2, 26 kPa, 5.9 g | 78 W/cm2, 80 MJ/m2, 14 kPa, 3.3 g | 87 W/cm2, 70 MJ/m2, 15 kPa, 3.6 g | 251 W/cm2, 219 MJ/m2, 9 kPa, 2.4 g | 309 W/cm2, 143 MJ/m2, 26 kPa, 6.2 g | 412 W/cm2, 247 MJ/m2, 23 kPa, 5.8 g | 525 W/cm2, 279 MJ/m2, 41 kPa, 10.2 g | 650 W/cm2, 284 MJ/m2, 74 kPa, 18.0 g |
| stock generic (reference) | ok 1630K 3% i309 | ok 1755K 13% i335 | ok 1801K 12% i327 | ok 2408K 39% i342 | ok 2561K 25% i322 | ok 2807K 42% i333 | ok 3037K 47% i334 | ok 3258K 46% i332 |
| cork-phenolic | ok 1444K 4% b300 i300 | ok 1529K 23% b302 i302 | ok 1565K 21% b302 i301 | FAIL overtemp @44s 13% b300 | FAIL overtemp @30s 8% b300 | FAIL overtemp @9s 2% b300 | FAIL overtemp @4s 1% b300 | FAIL overtemp @1s 0% b300 |
| phenolic-fiberglass | ok 1444K 2% b308 i303 | ok 1520K 10% b342 i313 | ok 1552K 9% b331 i310 | ok 1955K 34% b364 i315 | ok 2076K 24% b329 i308 | ok 2229K 43% b361 i312 | FAIL overtemp @6s 2% b300 | FAIL overtemp @3s 1% b300 |
| avcoat-5026-39 | ok 1527K 2% b302 i302 | ok 1590K 9% b312 i306 | ok 1623K 8% b309 i305 | ok 2024K 31% b318 i307 | ok 2144K 23% b308 i304 | ok 2294K 41% b317 i306 | ok 2442K 47% b320 i307 | ok 2568K 47% b319 i306 |
| sla-561v | ok 1412K 5% b301 i301 | ok 1491K 28% b304 i302 | ok 1524K 25% b303 i302 | ok 1958K 87% b401 i307 | ok 2098K 64% b306 i302 | FAIL overtemp @16s 11% b300 | FAIL overtemp @5s 3% b300 | FAIL overtemp @2s 1% b300 |
| carbon-phenolic | ok 1649K 0% b315 i304 | ok 1739K 1% b367 i316 | ok 1768K 1% b351 i313 | ok 2076K 6% b370 i316 | ok 2152K 5% b337 i309 | ok 2243K 9% b352 i312 | ok 2318K 11% b352 i312 | ok 2384K 12% b348 i311 |
| silica-tile-li900 | ok 1721K b301 g0.67 i301 | FAIL overtemp @183s b301 g0.10 | FAIL overtemp @106s b300 g0.36 | FAIL overtemp @15s b300 g0.93 | FAIL overtemp @11s b300 g0.95 | FAIL overtemp @5s b300 g0.98 | FAIL overtemp @2s b300 g0.99 | FAIL overtemp @1s b300 |
| pica | ok 1566K 1% b305 i302 | ok 1634K 7% b327 i309 | ok 1663K 7% b319 i307 | ok 1979K 33% b339 i310 | ok 2067K 26% b318 i305 | ok 2172K 49% b344 i309 | ok 2259K 58% b358 i309 | ok 2335K 59% b358 i309 |
| c-pica | ok 1565K 1% b303 i302 | ok 1633K 8% b316 i307 | ok 1661K 8% b312 i305 | ok 1979K 38% b326 i308 | ok 2067K 29% b311 i304 | ok 2172K 56% b334 i307 | ok 2258K 65% b351 i308 | ok 2335K 67% b353 i307 |
| tufroc | ok 1721K b303 i302 | ok 1872K b317 i308 | ok 1923K b313 g0.42 i306 | FAIL overtemp @21s b300 g0.77 | FAIL overtemp @16s b300 g0.84 | FAIL overtemp @6s b300 g0.97 | FAIL overtemp @3s b300 g0.99 | FAIL overtemp @1s b300 |
| heeet | ok 1637K 0% b310 i304 | ok 1725K 2% b349 i314 | ok 1755K 2% b337 i311 | ok 2063K 10% b352 i314 | ok 2143K 8% b327 i308 | ok 2237K 16% b341 i311 | ok 2312K 19% b341 i310 | ok 2378K 19% b338 i310 |
| intumescent-composite | ok 1383K 3% b303 i302 | ok 1473K 13% b315 i306 | ok 1510K 12% b311 i305 | ok 2010K 37% b325 i307 | ok 2173K 26% b311 i304 | ok 2385K 45% b323 i306 | ok 2562K 48% b326 i307 | FAIL overtemp @3s 1% b300 |
| reinforced-carbon-carbon | ok 1744K 0% b1107 i314 | ok 1904K 0% b1645 i355 | ok 1958K 1% b1634 i342 | FAIL overtemp @21s 0% b724 | FAIL overtemp @16s 0% b613 | FAIL overtemp @6s 0% b422 | FAIL overtemp @3s 0% b349 | FAIL overtemp @1s 0% b321 |

### light payload on a 3.75 m shield (2.5 t) on HeatShield3

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 59 W/cm2, 16 MJ/m2, 28 kPa, 5.9 g | 80 W/cm2, 82 MJ/m2, 15 kPa, 3.3 g | 89 W/cm2, 72 MJ/m2, 16 kPa, 3.6 g | 255 W/cm2, 227 MJ/m2, 10 kPa, 2.4 g | 310 W/cm2, 146 MJ/m2, 27 kPa, 6.1 g | 413 W/cm2, 253 MJ/m2, 24 kPa, 5.6 g | 525 W/cm2, 287 MJ/m2, 42 kPa, 9.9 g | 651 W/cm2, 292 MJ/m2, 76 kPa, 17.6 g |
| stock generic (reference) | ok 1634K 3% i309 | ok 1760K 14% i337 | ok 1806K 12% i328 | ok 2409K 40% i345 | ok 2554K 25% i324 | ok 2797K 43% i335 | ok 3026K 47% i337 | ok 3246K 47% i335 |
| cork-phenolic | ok 1452K 5% b300 i300 | ok 1538K 24% b302 i302 | ok 1575K 21% b302 i302 | FAIL overtemp @44s 13% b300 | FAIL overtemp @30s 8% b300 | FAIL overtemp @9s 2% b300 | FAIL overtemp @4s 1% b300 | FAIL overtemp @1s 0% b300 |
| phenolic-fiberglass | ok 1451K 2% b309 i303 | ok 1528K 10% b343 i313 | ok 1560K 9% b332 i310 | ok 1962K 35% b368 i316 | ok 2078K 25% b329 i308 | ok 2229K 44% b364 i313 | FAIL overtemp @6s 2% b300 | FAIL overtemp @3s 1% b300 |
| avcoat-5026-39 | ok 1535K 2% b302 i302 | ok 1598K 9% b313 i307 | ok 1631K 8% b309 i305 | ok 2032K 32% b319 i308 | ok 2147K 23% b308 i304 | ok 2294K 42% b318 i307 | ok 2442K 48% b322 i307 | ok 2568K 49% b321 i307 |
| sla-561v | ok 1420K 6% b301 i301 | ok 1500K 29% b304 i303 | ok 1533K 26% b303 i302 | ok 1967K 90% b527 x10% g0.97 i309 | FAIL overtemp @46s 30% b300 | FAIL overtemp @16s 11% b300 | FAIL overtemp @5s 3% b300 | FAIL overtemp @2s 1% b300 |
| carbon-phenolic | ok 1657K 0% b315 i304 | ok 1746K 1% b368 i317 | ok 1775K 1% b351 i313 | ok 2081K 6% b372 i318 | ok 2153K 5% b337 i310 | ok 2243K 10% b354 i313 | ok 2318K 11% b354 i313 | ok 2384K 12% b350 i312 |
| silica-tile-li900 | ok 1734K b301 g0.50 i301 | FAIL overtemp @182s b301 g0.12 | FAIL overtemp @106s b300 g0.37 | FAIL overtemp @15s b300 g0.93 | FAIL overtemp @11s b300 g0.95 | FAIL overtemp @5s b300 g0.98 | FAIL overtemp @2s b300 g0.99 | FAIL overtemp @1s b300 |
| pica | ok 1573K 1% b305 i303 | ok 1641K 7% b327 i310 | ok 1670K 7% b320 i308 | ok 1985K 35% b342 i311 | ok 2070K 27% b318 i306 | ok 2172K 50% b347 i310 | ok 2259K 59% b363 i311 | ok 2335K 61% b364 i310 |
| c-pica | ok 1573K 1% b303 i302 | ok 1640K 8% b317 i307 | ok 1669K 8% b312 i305 | ok 1984K 39% b328 i308 | ok 2070K 30% b312 i304 | ok 2172K 57% b336 i308 | ok 2258K 67% b357 i309 | ok 2335K 69% b360 i308 |
| tufroc | ok 1733K b303 i302 | ok 1885K b318 g0.97 i308 | ok 1936K b313 x10% g0.00 i307 | FAIL overtemp @21s b300 g0.77 | FAIL overtemp @16s b300 g0.84 | FAIL overtemp @6s b300 g0.97 | FAIL overtemp @3s b300 g0.99 | FAIL overtemp @1s b300 |
| heeet | ok 1645K 0% b311 i304 | ok 1732K 2% b350 i315 | ok 1762K 2% b337 i312 | ok 2068K 10% b354 i316 | ok 2145K 8% b328 i309 | ok 2237K 16% b342 i312 | ok 2312K 19% b343 i312 | ok 2378K 20% b340 i311 |
| intumescent-composite | ok 1391K 3% b303 i302 | ok 1483K 13% b316 i307 | ok 1521K 12% b311 i305 | ok 2021K 38% b326 i308 | ok 2179K 27% b311 i304 | ok 2385K 46% b325 i307 | ok 2562K 50% b328 i307 | FAIL overtemp @3s 1% b300 |
| reinforced-carbon-carbon | ok 1757K 0% b1116 i315 | ok 1919K 0% b1657 i361 | ok 1972K 2% b1647 i347 | FAIL overtemp @21s 0% b724 | FAIL overtemp @16s 0% b613 | FAIL overtemp @6s 0% b422 | FAIL overtemp @3s 0% b349 | FAIL overtemp @1s 0% b321 |

### 10 m inflatable (12 t) on InflatableHeatShield

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 34 W/cm2, 10 MJ/m2, 9 kPa, 5.4 g | 44 W/cm2, 47 MJ/m2, 5 kPa, 3.0 g | 49 W/cm2, 41 MJ/m2, 5 kPa, 3.3 g | 92 W/cm2, 11 MJ/m2, 0 kPa, 0.3 g | 106 W/cm2, 11 MJ/m2, 1 kPa, 0.4 g | 220 W/cm2, 10 MJ/m2, 0 kPa, 0.3 g | 420 W/cm2, 9 MJ/m2, 0 kPa, 0.2 g | 614 W/cm2, 9 MJ/m2, 0 kPa, 0.1 g |
| ftps-nextel-gen1 | ok 1450K b340 i302 | ok 1725K b435 i309 | ok 1776K b410 i307 | FAIL overtemp @24s b301 | FAIL overtemp @20s b301 | FAIL overtemp @10s b300 | FAIL overtemp @6s b300 | FAIL overtemp @3s b300 |
| ftps-sic-gen2 | ok 1449K b313 i301 | ok 1716K b348 i305 | ok 1767K b339 i304 | FAIL overtemp @28s b300 | FAIL overtemp @23s b300 | FAIL overtemp @11s b300 | FAIL overtemp @6s b300 | FAIL overtemp @3s b300 |
| ftps-aerogel-gen3 | ok 1441K b310 i301 | ok 1705K b337 i304 | ok 1756K b330 i303 | FAIL overtemp @34s b301 | FAIL overtemp @26s b300 | FAIL overtemp @12s b300 | FAIL overtemp @6s b300 | FAIL overtemp @4s b300 |
| adept-carbon-fabric | ok 1416K 0% b1007 i310 | ok 1685K 6% b1510 i336 | ok 1736K 6% b1526 i329 | ok 2397K 58% b2045 i337 | FAIL overtemp @34s 9% b1292 | FAIL overtemp @13s 2% b651 | FAIL overtemp @7s 1% b452 | FAIL overtemp @4s 0% b392 |

### 10 m inflatable (4 t) on InflatableHeatShield

| Material | Suborbital 1.5 | LKO shallow | LKO steep | Mun return Pe35 | Mun return Pe15 | Interplanetary 4.2 | High energy 5.5 | Extreme 7.0 |
|---|---|---|---|---|---|---|---|---|
| *environment (first row's case)* | 23 W/cm2, 7 MJ/m2, 3 kPa, 4.9 g | 28 W/cm2, 30 MJ/m2, 2 kPa, 2.8 g | 32 W/cm2, 26 MJ/m2, 2 kPa, 3.1 g | 89 W/cm2, 11 MJ/m2, 0 kPa, 0.6 g | 103 W/cm2, 11 MJ/m2, 1 kPa, 0.9 g | 218 W/cm2, 10 MJ/m2, 0 kPa, 0.7 g | 419 W/cm2, 9 MJ/m2, 0 kPa, 0.5 g | 613 W/cm2, 9 MJ/m2, 0 kPa, 0.3 g |
| ftps-nextel-gen1 | ok 1186K b337 i302 | ok 1531K b413 i307 | ok 1583K b393 i306 | FAIL overtemp @25s b301 | FAIL overtemp @21s b301 | FAIL overtemp @10s b300 | FAIL overtemp @6s b300 | FAIL overtemp @3s b300 |
| ftps-sic-gen2 | ok 1193K b313 i301 | ok 1528K b341 i304 | ok 1579K b333 i303 | FAIL overtemp @28s b301 | FAIL overtemp @23s b300 | FAIL overtemp @11s b300 | FAIL overtemp @6s b300 | FAIL overtemp @3s b300 |
| ftps-aerogel-gen3 | ok 1180K b310 i301 | ok 1514K b331 i303 | ok 1564K b325 i303 | FAIL overtemp @38s b301 | FAIL overtemp @27s b300 | FAIL overtemp @12s b300 | FAIL overtemp @6s b300 | FAIL overtemp @4s b300 |
| adept-carbon-fabric | ok 1155K 0% b856 i309 | ok 1488K 2% b1338 i330 | ok 1540K 2% b1347 i324 | ok 2201K 27% b1796 i328 | ok 2312K 24% b1740 i322 | FAIL overtemp @13s 2% b657 | FAIL overtemp @7s 1% b452 | FAIL overtemp @4s 0% b392 |
