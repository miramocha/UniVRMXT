# Cursor configuration

Agent guidance for the **UniVRMXT** UPM package (`com.miramocha.univrmxt`).

## Shared kit (from VRMXT Plugin for Warudo)

Shared Unity rules and `validate-unity-meta` are synced from sibling Warudo plugin:

```powershell
cd "../VRMXT Plugin for Warudo"
./scripts/sync-vrmxt-cursor-shared.ps1 -Apply
```

## Local only (do not overwrite via sync)

| File | Role |
|------|------|
| `rules/unity-csharp-language.mdc` | Unity **2022.3** pin (matches `package.json`) |
| `rules/univrmxt-repository.mdc` | UPM package host layout |

## Project assumptions

- Unity floor: `2022.3` (see `package.json`)
- Optional Extended VRM extensions on stock UniVRM (`VRMXT_sprite_particle`,
  `VRMXT_materials_override`)
- C# and documentation use LF line endings

## Deliberately not included

- Warudo Mod Tool / ui-labels kits
- Extended-UniVRM fork agent config (none — upstream fork stays clean)
- VRMXT Unity Player app scenes and WebGL bridge
- GridDungeon UITK / backlog / story Cursor kits
