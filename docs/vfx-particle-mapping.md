# VRMXT_vfx → Unity ParticleSystem mapping

Portable fields from [VRMXT_vfx](https://github.com/miramocha/Extended-VRM-Specs/blob/main/specs/vrmxt-vfx.md)
map onto Unity `ParticleSystem` via `VrmxtVfxParticleSystemMapper`. Spec defaults apply when
JSON omits a property (`VrmxtVfx.Default*`).

## Field table

| Spec field | Unity target | Notes |
|------------|--------------|-------|
| `emissionRate` | `ParticleSystem.emission.rateOverTime` | Particles per second |
| `maxParticles` | `main.maxParticles` | Clamped to ≥ 1 |
| `lifetime` | `main.startLifetime` | Seconds |
| `startSize` | `main.startSize` | Meters (billboard size) |
| `startSpeed` | `velocityOverLifetime.y` (local) | `main.startSpeed` forced to `0`; velocity along emitter local **+Y** |
| `startColor` | `main.startColor` | Linear RGBA |
| `texture` | `ParticleSystemRenderer.material.mainTexture` | Index into glTF `textures[]` via caller `resolveTexture` |
| (billboard) | `renderer.renderMode = Billboard`, `alignment = View` | Camera-facing when supported |
| `localPosition` / `localRotation` | Child transform under node | Parent = resolved `Nodes[node]` |

## Texture policy

1. When `texture` is set and `resolveTexture(index)` returns a non-null `Texture`, assign it to the particle material main texture.
2. When `texture` is omitted, out of range, or unresolved (`null`), keep the default particle material and tint with `startColor` (solid-tint fallback).

UniVRMXT Runtime does not decode glTF images itself. After `Vrm10.LoadGltfDataAsync`, pass textures from the loaded avatar (for example textures already imported onto materials, or a project-specific `textures[]` → `Texture2D` map).

## Defaults (when properties omitted)

| Field | Default |
|-------|---------|
| `emissionRate` | `10` |
| `maxParticles` | `64` |
| `lifetime` | `1` |
| `startSize` | `0.05` |
| `startSpeed` | `0.1` |
| `startColor` | `[1,1,1,1]` |
| `localPosition` | `[0,0,0]` |
| `localRotation` | `[0,0,0,1]` (xyzw) |
| `texture` | none (tint fallback) |

## Call site (post UniVRM load)

```csharp
using var data = new GlbLowLevelParser(path, File.ReadAllBytes(path)).Parse();
var vrm = await Vrm10.LoadGltfDataAsync(data);
var nodes = vrm.GetComponent<RuntimeGltfInstance>().Nodes;

// Data only:
VrmxtVfxRuntime.TryAttach(vrm.gameObject, data.Json, nodes, out var vfx);

// Data + ParticleSystem children (texture resolver optional):
VrmxtVfxRuntime.TryAttach(
    vrm.gameObject,
    data.Json,
    nodes,
    index => ResolveGltfTexture(index), // return null → solid tint
    out vfx);
```

Missing `extensions.VRMXT_vfx` → `TryAttach` returns `false` (no-op). Unresolved nodes skip that emitter only.
