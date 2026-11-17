# Contributing to RealTPS

Thanks for wanting to help! Contributions I'd especially love to see:

- **support for other mods' heat shields and pods** (usually just one config entry);
- **translations** (only English is required, any other language is welcome);
- **materials**, as long as they come with real sources for their numbers;
- **bug fixes**, with a test that shows the bug.

If you want to change how the model works (the physics, the damage, the failure rules), please open an issue first
so we can talk about it before you spend time on it.

## Ground rules

- **Language.** Code, configs, documents and commit messages are in English. In-game text ships in `en-us`
  (required) and any other language you like.
- **No copied content.** No code, models, textures or configs from other mods, and no decompiled game code.
  Dependencies are referenced, never copied into the repository.
- **Every number has a source.** If you change a material number or add a new one, it gets its row in
  `docs/sources/MATERIAL_SOURCES.md`, with its status (published value, derived, assumption, gameplay calibration
  or fictional).
- **Don't claim a test you didn't run.** Say what you tested in the game, or say that you didn't. Results from the
  numerical model are never presented as flight results.
- **Your KSP install is only read.** The build compiles against it; test in a copy of the game you don't mind
  breaking.

## What you need

- The **.NET 8 SDK**.
- A **KSP 1.12.x install**. It's only read: the plugin compiles against it, and you test in it.

## Setting up (once)

Tell the build where your KSP is: copy `src/RealTPS.KSP/RealTPS.KSP.csproj.user.example` to
`RealTPS.KSP.csproj.user` (same folder) and put your path in it. Git ignores that file, so each machine keeps its
own. Setting the `KSP_ROOT` environment variable works too.

## Repository layout

| Path | What's in it |
|---|---|
| `GameData/RealTPS/` | What ships: configs, localization and (after a build) the plugin |
| `data/hosts/` | Which parts RealTPS enrols, as input for the generator. Not shipped |
| `src/RealTPS.Core/` | The model: config reader, materials, solver, generator. No KSP or Unity references |
| `src/RealTPS.KSP/` | The KSP plugin (`RealTPSRuntime.dll`), which includes the model's code |
| `tests/RealTPS.Core.Tests/` | Unit tests |
| `tools/RtpsTool/` | `validate`, `generate`, `simulate`, `maps` |
| `docs/wiki/` | The player guide, the modding reference (`Modding.md`) and the commented `example.cfg` |
| `docs/sources/` | Where every material number comes from, and the numerical mission model |
| `netkan/` | The CKAN metadata |
| `.github/` | CI, the release workflow, issue forms and the pull request template |

## The dotnet commands

Run them from the repository root. `RealTPS.sln` there holds the four projects, so `dotnet test` and
`dotnet build` take everything at once (it's also what you open in Visual Studio or Rider).

| Command | What it does | When |
|---|---|---|
| `dotnet test` | Compiles everything, plugin included, and runs the unit tests | Before every pull request |
| `dotnet run --project tools/RtpsTool -- validate` | Checks the materials, the hosts, the tech tree files, the difficulty presets, the localization and `MATERIAL_SOURCES.md`, and reports errors and warnings | After any config change |
| `dotnet run --project tools/RtpsTool -- generate` | Writes the three generated files from the materials and `data/hosts/` | After changing a material or a host |
| `dotnet run --project tools/RtpsTool -- generate --check` | Only checks that the generated files are up to date | Before every pull request |
| `dotnet run --project tools/RtpsTool -- simulate` | Runs the numerical mission model and prints a table (`--tilt N`: face tilted N degrees; `--preset NAME`: a difficulty preset) | After a balance change |
| `dotnet run --project tools/RtpsTool -- maps` | Draws example images of the shield map (`--out DIR`, default `artifacts/maps`) | After changing the window's map |
| `dotnet build -c Release` | Compiles everything and copies `RealTPSRuntime.dll` into `GameData/RealTPS/Plugins` | To test in the game |

The RtpsTool commands still name the project, because `dotnet run` works on one project, not on the whole
solution.

Only a Release build copies the DLL into `GameData`. A Debug build, which is what `dotnet test` and a plain
`dotnet build` do, never touches it.

No KSP on this machine? `dotnet test tests/RealTPS.Core.Tests` runs the tests without the plugin, and the
RtpsTool commands don't need KSP either.

**Never commit `RealTPSRuntime.dll`.** Git ignores it, and each release compiles it from its own source.

## How the configs are built

Some config files are written by hand, and others are generated from them:

```
GameData/RealTPS/Materials/*.cfg  ─┐
                                   ├─ RtpsTool generate ─►  GameData/RealTPS/Patches/StockHosts.cfg
data/hosts/*.cfg  ─────────────────┘                        GameData/RealTPS/Resources/MaterialResources.cfg
                                                            GameData/RealTPS/TechTree/MaterialUnlocks.cfg
```

**The only config files you edit by hand:**

| File | What's in it |
|---|---|
| `GameData/RealTPS/Materials/*.cfg` | The materials (`RTPS_MATERIAL`) |
| `data/hosts/*.cfg` | The parts RealTPS enrols (`RTPS_HOST`), one file per mod |
| `GameData/RealTPS/TechTree/<tree>.cfg` | Unlock nodes for another tech tree (for now, `CommunityTechTree.cfg`); every file there except `MaterialUnlocks.cfg` |
| `GameData/RealTPS/Localization/<language>.cfg` | In-game text, one file per language |
| `GameData/RealTPS/Difficulty/DifficultyPresets.cfg` | The values of the Friendly, Realistic and Strict presets |
| `GameData/RealTPS/RealTPS.version` | The version, for KSP-AVC and CKAN (only changed for releases) |

**Generated, never edited by hand:** `Patches/StockHosts.cfg`, `Resources/MaterialResources.cfg` and
`TechTree/MaterialUnlocks.cfg`. `generate --check` and a unit test fail when they're out of date.

Every parameter of every kind of config is explained, with an example, in `docs/wiki/example.cfg` (a unit test
keeps it valid) and in the [modding reference](docs/wiki/Modding.md).

## Recipes

**Add a part (stock or from another mod) to RealTPS**

1. Add an `RTPS_HOST` to `data/hosts/<mod>.cfg` (a new file for another mod, with `needs = <ModName>`), listing
   its materials, with one that needs no unlock first.
2. Run `generate`, then `validate`, then the tests.

Another mod can also enrol its own parts without touching RealTPS, with a part patch (section 3 of
`docs/wiki/example.cfg`).

**Add a material**

1. Add an `RTPS_MATERIAL` to the right file in `GameData/RealTPS/Materials/`, with `speculative = true` if it's
   theoretical or fictional.
2. Add its rows to `docs/sources/MATERIAL_SOURCES.md`.
3. Add its texts (`_title`, `_desc`, `_detail`) to `Localization/en-us.cfg`. Every number in the `_detail` line
   has to be one of the material's values, rounded as shown; `validate` points out a stale one.
4. List it on the hosts that should carry it (`data/hosts/`).
5. Run `generate`, `validate` and the tests; then rerun `simulate` and update
   `docs/sources/MISSION_TEST_MATRIX.md`.

**Change a material's number**

Change it in the material file, in its row of `MATERIAL_SOURCES.md` and in the `_detail` line of every language
that has one. Then run `generate`, `validate` and the tests.

**Support another tech tree**

Add `GameData/RealTPS/TechTree/<the tree's ModuleManager name>.cfg` with one patch for each material whose node
changes (section 6 of `docs/wiki/example.cfg`). `validate` checks the format, but not whether the node exists in
that tree, so test it in the game.

**Add or fix a translation**

Copy `en-us.cfg` to `<language>.cfg`, rename the `en-us` node and translate. Keep every tag and every `<<n>>`
placeholder. A tag missing from another language is only a warning (players of that language then see the raw
tag), but a different placeholder or a stale number is an error.

**Retune the difficulty presets**

Edit `Difficulty/DifficultyPresets.cfg`; its header lists every key and its range. Players and other mods can
patch it instead (section 5 of `docs/wiki/example.cfg`).

## Changing code

- The model in `src/RealTPS.Core` has no KSP or Unity references and is covered by unit tests. Physics goes there,
  not in the KSP plugin.
- C# 7.3; warnings are errors; no new dependencies without a reason in the pull request.
- A behaviour change needs a test that fails without it.

## Pull requests

The pull request template has the checklist. On GitHub, CI runs the tests, `validate` and `generate --check`, and
compiles the plugin against the stripped KSP assemblies the community publishes for this
(KSPModdingLibs/KSPLibs).

## Releases (for me)

1. Set the new version in `GameData/RealTPS/RealTPS.version` and in
   `src/RealTPS.KSP/Properties/AssemblyInfo.cs`, and add the changes to `CHANGELOG.md`.
2. If you want, run the **Release** workflow by hand (Actions tab) first: it builds the zip without publishing
   anything, so you can check it and test it in the game.
3. Publish a GitHub release tagged with the version and a `v` in front (`v0.20.0` for 0.20.0). The workflow checks
   that the three versions agree, compiles the plugin, builds `RealTPS-<version>.zip` and attaches it to the
   release, where CKAN picks it up.

Publishing, changing the repository's visibility and submitting to CKAN are my call.
