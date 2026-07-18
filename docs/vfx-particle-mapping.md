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
| `texture` | Owned particle material `_MainTex` + `_BaseMap` | Index into glTF `textures[]` via caller `resolveTexture` |
| (billboard) | `renderer.renderMode = Billboard`, `alignment = View` | Camera-facing when supported |
| `localPosition` / `localRotation` | Child transform under node | Parent = resolved `Nodes[node]` |

## Texture / material policy (BIRP + URP)

1. Mapper creates an owned unlit particle material for the active pipeline:
   - URP → `Universal Render Pipeline/Particles/Unlit` (type-name detect, no URP asmdef)
   - BIRP → `Particles/Standard Unlit`
   - Last resort → `Sprites/Default`
2. When `texture` resolves, set both `_MainTex` (BIRP) and `_BaseMap` (URP) when the shader exposes them.
3. When `texture` is omitted, out of range, or unresolved (`null`), leave albedo default and tint with `startColor` (solid-tint fallback).
4. HDRP is best-effort only (no dedicated particle shader pick yet).

### UniVRM does not import VFX-only textures

Stock UniVRM only enumerates textures referenced by materials / meta thumbnail. A
`textures[]` entry used only by `VRMXT_vfx` is skipped. Workaround without upstream:

```csharp
// After Vrm10.LoadGltfDataAsync — re-read the same file bytes
var bytes = File.ReadAllBytes(path);
var nodes = vrm.GetComponent<RuntimeGltfInstance>().Nodes;
VrmxtVfxRuntime.TryAttachFromGlb(vrm.gameObject, bytes, nodes, out var vfx, out var glbTextures);
// Dispose glbTextures when destroying the avatar (or ReleaseOwnership if saved into an asset)
```

`VrmxtVfxGlbTextures` decodes embedded PNG/JPEG from the GLB BIN chunk via
`GltfImageBytes` + `Texture2D.LoadImage`.

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
var bytes = File.ReadAllBytes(path);
using var data = new GlbLowLevelParser(path, bytes).Parse();
var vrm = await Vrm10.LoadGltfDataAsync(data);
var nodes = vrm.GetComponent<RuntimeGltfInstance>().Nodes;

// Preferred: second use of bytes decodes VFX-only textures UniVRM skipped
VrmxtVfxRuntime.TryAttachFromGlb(
    vrm.gameObject, bytes, nodes, out var vfx, out var glbTextures);
```

Missing `extensions.VRMXT_vfx` → `TryAttach` returns `false` (no-op). Unresolved nodes skip that emitter only.

## AssetDatabase `.vrm` import

UniVRM `VrmScriptedImporter` does not call UniVRMXT, and its main asset rejects
`AddComponent` inside `AssetPostprocessor`. Workflow:

1. Import / reimport the `.vrm` as usual (avatar only on that asset).
2. Postprocessor re-reads the file, decodes VFX textures, writes sibling **`*.vrmxt.prefab`**.
3. Place the **`.vrmxt.prefab`** in the scene (not the raw `.vrm`).

Example: `Assets/vfx_smoke.vrm` → `Assets/vfx_smoke.vrmxt.prefab`.
