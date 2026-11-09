# PinnedMaterials ONI Mod

Oxygen Not Included mod (work in progress).

## Local build setup

Build settings follow the `CritterCensus` layout.

1. Copy `PinnedMaterials/Local.Build.props.example` to `PinnedMaterials/Local.Build.props` (git-ignored).
2. Set `GameManagedPath` (`OxygenNotIncluded_Data\Managed`) and `ModInstallPath` (e.g. `...\mods\local\PinnedMaterials`).
   Alternatively set the environment variables `ONI_GAME_MANAGED_PATH` / `ONI_MOD_INSTALL_PATH`.
3. `dotnet build` — the DLL, pdb, `mod.yaml`, `mod_info.yaml` and `translations/*.po` are copied to `ModInstallPath` after each build.
