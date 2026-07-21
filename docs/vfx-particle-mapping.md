# VRMXT_sprite_particle → Unity ParticleSystem mapping

Portable fields from [VRMXT_sprite_particle](https://github.com/miramocha/Extended-VRM-Specs/blob/main/specs/extensions/vfx/vrmxt-sprite-particle.md)
map onto Unity `ParticleSystem` via `VrmxtVfxParticleSystemMapper`. Spec defaults apply when
JSON omits a property (`VrmxtVfx.Default*`).

## Field table

| Spec field | Unity target | Notes |
|------------|--------------|-------|
| `emissionRate` | `ParticleSystem.emission.rateOverTime` | Particles per second |
| `maxParticles` | `main.maxParticles` | Clamped to ≥ 1 |
| `lifetime` | `main.startLifetime` | Seconds |
| `size[0]` / `size[1]` | `main.startSizeX` / `main.startSizeY` (3D mode) | World meters; divided by parent `lossyScale` so sprite dimensions do not inherit node scale |
| `startSpeed` | `velocityOverLifetime.y` (local) | `main.startSpeed` forced to `0`; velocity along emitter local **+Y** |
| `color` | `main.startColor` | Linear RGBA; export reads live PS value back |
| `texture` | Owned particle material `_MainTex` + `_BaseMap` | Index into glTF `textures[]` via caller `resolveTexture`; structurally invalid index skips emitter; decode failure → white solid × `color` |
| (billboard) | `renderer.renderMode = Billboard`, `alignment = View` | Camera-facing when supported |
| attach `node` | Child `ParticleSystem` parent | Identity local TR; offsets live on helper glTF nodes |

## Export sync

Before writing `VRMXT_sprite_particle`, `VrmxtVfxExporter.CaptureAndClearParticleSystems` calls
`VrmxtVfxParticleSystemMapper.ReadFromParticleSystem` so Unity inspector edits on the
preview `ParticleSystem` (color, rate, lifetime, size, speed) fold into portable emitter
data. Albedo is taken from `VrmxtVfxParticleData.Texture` (set on import bind / material
sync) so export still embeds textures after preview systems are cleared.

`VrmxtVfxInstance.OnValidate` pushes emitter fields onto existing preview ParticleSystems
via `SyncParticleSystemsFromEmitters`. `[ExecuteAlways]` `Update` pulls PS inspector edits
back into emitters via `SyncEmittersFromParticleSystems`.

## Texture / material policy (BIRP + URP)

Same as prior VFX notes: owned unlit particle material, transparent alpha blend, optional
`PackagedMaterialProvider` for Warudo/UMod.

### UniVRM does not import VFX-only textures

Stock UniVRM only enumerates textures referenced by materials / meta thumbnail. Use
`VrmxtVfxRuntime.TryAttachFromGlb` to re-read GLB bytes and decode extension textures.

## Defaults (when properties omitted)

| Field | Default |
|-------|---------|
| `emissionRate` | `10` |
| `maxParticles` | `64` |
| `lifetime` | `1` |
| `size` | `[0.05, 0.05]` |
| `startSpeed` | `0.1` |
| `color` | `[1,1,1,1]` |
| `texture` | none (white solid × `color`) |

## Call site (post UniVRM load)

```csharp
VrmxtVfxRuntime.TryAttachFromGlb(
    vrm.gameObject, bytes, nodes, out var vfx, out var glbTextures);
```

Missing `extensions.VRMXT_sprite_particle` → `TryAttach` returns `false` (no-op). Unresolved
nodes or structurally invalid texture indices skip that emitter only.

## AssetDatabase `.vrm` import

Dual path unchanged — see [univrm-upstream-hooks.md](https://github.com/miramocha/Extended-VRM-Specs/blob/main/implementations/univrm-upstream-hooks.md).
