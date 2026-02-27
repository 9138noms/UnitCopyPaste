# UnitCopyPaste

BepInEx mod for group copy/paste of units in the Nuclear Option mission editor.

## Features
- **Ctrl+C** — Copy selected unit(s) (single or multi-select)
- **Ctrl+V** — Paste group at mouse cursor position (relative positions preserved)
- **Ctrl+D** — Duplicate in place (10m offset)

All unit properties are copied: unit type, faction, loadout, waypoints, fuel, etc.

## Installation
1. Install [BepInEx 5.x](https://github.com/BepInEx/BepInEx)
2. Copy `UnitCopyPaste.dll` to `BepInEx/plugins/`

## Known Issues
- Aircraft liveries (skins) are not copied
- Some objects (decorations, cover) may spawn slightly buried in terrain — nudge them in the editor to auto-correct
- Ships spawn 35m above water and drop down

## Build
```
dotnet build -c Release
```

## Requirements
- Nuclear Option
- BepInEx 5.x
- .NET Framework 4.7.2
