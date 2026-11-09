# Pinned Materials

An Oxygen Not Included mod that lets you **pin** materials in the build menu's material list, so the ones you use
most are always shown first and you no longer have to hunt for them.

## Features

- **Pin / unpin**: every material button has a small pin icon in the bottom-right corner of its icon.
  Hover a material to reveal the pin, click it to pin; click a pinned material's pin to unpin.
- **Pinned materials first**: pinned materials are sorted to the top of the material list, in the order you pinned
  them. The remaining materials keep the game's normal order.
- **Drag to reorder**: drag a pinned material's pin icon onto another pinned material to move it to that position.
  A copy of the icon follows the cursor and the dragged material is dimmed in its current slot so you can see
  where it will land. The order is saved when you drop it.
- **Global pins**: pins apply to materials, not to buildings. A pinned material goes first in every building's list
  and in every material slot of buildings that need more than one.
- **Persistent**: pins are stored per PC (not per save game) and survive restarts.
- **Same rules as the game**: materials you have not discovered yet stay hidden, and pinned materials that are out of
  stock are still listed (greyed out) at the top. Initial selection is unchanged (the game still restores your last
  choice), so pinning only affects ordering.

## Pin icons

The pin icons are loaded from the mod folder, so you can replace them:

| File | Shown when |
|---|---|
| `ModAssets/pinned.png` | the material is pinned (always visible) |
| `ModAssets/unpinned.png` | the material is not pinned (only while hovering the material) |

Use transparent PNGs, roughly 32x32 (they are displayed at 16x16). If a file is missing, a built-in pin glyph is
used instead. Images are shown as-is, without tinting.

## Settings file

Pinned materials are saved as a JSON list, in pin order, to `mods/config/PinnedMaterials.json` inside the game's
user data folder. Delete the file to clear all pins.

## Translations

Tooltips are translated through `translations/<language code>.po` (English and Japanese are included).

## Compatibility

- ONI build: **744825 or later**
- Required DLC: none
- Patches `MaterialSelector.ElementSorter` (Harmony postfix) and `MaterialSelector.ConfigureScreen`.

## Local build setup

Build settings follow the `CritterCensus` layout.

1. Copy `PinnedMaterials/Local.Build.props.example` to `PinnedMaterials/Local.Build.props` (git-ignored).
2. Set `GameManagedPath` (`OxygenNotIncluded_Data\Managed`) and `ModInstallPath` (e.g. `...\mods\local\PinnedMaterials`).
   Alternatively set the environment variables `ONI_GAME_MANAGED_PATH` / `ONI_MOD_INSTALL_PATH`.
3. `dotnet build` — the DLL, pdb, `mod.yaml`, `mod_info.yaml`, `translations/*.po` and `ModAssets/*.png` are copied to
   `ModInstallPath` after each build.
