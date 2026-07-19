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
| `startColor` | `main.startColor` | Linear RGBA; export reads live PS value back |
| `texture` | Owned particle material `_MainTex` + `_BaseMap` | Index into glTF `textures[]` via caller `resolveTexture` |
| (billboard) | `renderer.renderMode = Billboard`, `alignment = View` | Camera-facing when supported |
| `localPosition` / `localRotation` | Child transform under node | Parent = resolved `Nodes[node]` |

## Export sync

Before writing `VRMXT_vfx`, `VrmxtVfxExporter.CaptureAndClearParticleSystems` calls
`VrmxtVfxParticleSystemMapper.ReadFromParticleSystem` so Unity inspector edits on the
preview `ParticleSystem` (color, rate, lifetime, size, speed, local TR) fold into portable
emitter data. Albedo is taken from `VrmxtVfxParticleData.Texture` (set on import bind /
material sync) so export still embeds textures after preview systems are cleared.
Editing only `VrmxtVfxInstance` fields also works when no PS child exists.

`VrmxtVfxInstance.OnValidate` pushes emitter fields onto existing preview ParticleSystems
via `SyncParticleSystemsFromEmitters` (no destroy/rebuild).

## Texture / material policy (BIRP + URP)

1. Mapper creates an owned unlit particle material:
   - Probe host **URP and BIRP** particle shader names (order hinted by
     `GraphicsSettings.currentRenderPipeline == null`, but both are always tried).
     Do not pick the packaged shader while a host URP/BIRP name still resolves — during
     ScriptedImporter the pipeline asset is often null even in URP projects.
   - Then packaged **`VRMXT/Particles Unlit`** (first-party ShaderLab; no URP package
     dependency). Material at `Resources/UniVRMXT/ParticlesUnlit` keeps that shader in
     player builds when the UniVRMXT package is present. Hosts that cannot use
     `Resources.Load` (e.g. Warudo/UMod) set `PackagedMaterialProvider` +
     `PreferPackagedParticleMaterial` after loading the mat via ModHost.
   - If `Shader.Find` fails: clone the Resources material, else default `ParticleSystem`
     material / `Default-Particle`.
2. Owned materials are forced to **Transparent + Alpha blend** (`ConfigureTransparentAlphaBlending`)
   when the host shader exposes URP/BIRP surface keywords. The packaged shader is already
   transparent in ShaderLab.
   PNG alpha from GLB `LoadImage` is fine; missing transparency on host URP Particles/Unlit
   was a material surface setting (defaults to Opaque when created from script).
3. When `texture` resolves, set both `_MainTex` (BIRP) and `_BaseMap` (URP) when the shader exposes them.
4. When `texture` is omitted, out of range, or unresolved (`null`), leave albedo default and tint with `startColor` (solid-tint fallback).
5. HDRP is best-effort (packaged ShaderLab pass or host fallbacks; no dedicated HDRP particle shader).
6. **Import persistence:** decode textures, `AddObjectToAsset` them **first**, re-bind onto
   owned particle materials, then `AddObjectToAsset` the materials (or embed in the companion
   prefab). Adding materials before textures drops texture slots on serialize (empty albedo).

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

Dual path:

| Host | What to use in scenes |
|------|------------------------|
| **Extended-UniVRM** + Project Settings/VRM10 → Enable VRM Import Extensions | Raw **`.vrm`** — VFX attached during ScriptedImporter |
| **Stock UniVRM**, or Extended with import extensions **disabled** | Sibling **`*.vrmxt.prefab`** (postprocessor fallback) |

Reimport `.vrm` after changing UniVRMXT, Extended-UniVRM, or the Project Settings toggle. Details:
[univrm-upstream-hooks.md](https://github.com/miramocha/Extended-VRM-Specs/blob/main/implementations/univrm-upstream-hooks.md).
