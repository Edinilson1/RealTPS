## What this changes

<!-- One or two sentences. Link the issue it closes, if any. -->

<!-- Please don't include the compiled RealTPSRuntime.dll, only the source. Each release compiles the plugin
     from its own source, and CI checks that it builds. -->

## Checklist (CONTRIBUTING.md)

- [ ] `dotnet test` passes
- [ ] `dotnet run --project tools/RtpsTool -- validate` reports 0 errors
- [ ] `dotnet run --project tools/RtpsTool -- generate --check` reports everything up to date
- [ ] No compiled DLL in the changes
- [ ] A changed or new material number has its row in `docs/sources/MATERIAL_SOURCES.md`
- [ ] New or changed in-game text is in `en-us.cfg` (required). Other languages are optional: a missing
      translation is only a warning, but a number changed in a material detail line must be changed in every
      language file that has that line
- [ ] If balance changed: `simulate` rerun and `docs/sources/MISSION_TEST_MATRIX.md` updated
- [ ] Tested in the game (say what), or marked as not tested
