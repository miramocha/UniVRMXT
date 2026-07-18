# Architecture

UniVRMXT is an optional consumer package for [Extended VRM](https://github.com/miramocha/Extended-VRM-Specs) glTF extensions. Normative behavior is defined in that repository; this package implements Unity-side parsing and integration hooks.

## Layering

| Layer | Location | Depends on |
|-------|----------|------------|
| Format | `Runtime/Format/` | `Newtonsoft.Json` only |
| VFX runtime | `Runtime/Vfx/` | Format, UnityEngine |
| Materials override | `Runtime/MaterialsOverride/` | Format, UnityEngine |
| Editor integration | `Editor/` | Runtime |
| Tests | `Tests/Format/`, `Tests/Vfx/` | Runtime (Editor, NUnit) |

The Format layer parses extension JSON with Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`)
and does not reference UniGLTF types, so format tests stay free of UniVRM load APIs.

## Extensions (v0.1.0)

### VRMXT_vfx

- Root extension: `extensions.VRMXT_vfx`
- Spec: [vrmxt-vfx.md](https://github.com/miramocha/Extended-VRM-Specs/blob/main/specs/vrmxt-vfx.md)
- `VrmxtVfx.TryParse` validates `specVersion` `1.0`, applies particle defaults, and skips invalid emitters.
- `VrmxtVfxImporter.TryImport` maps emitters to Unity data; Transform overloads skip unresolved nodes.
- `VrmxtVfxRuntime.TryAttach` adds `VrmxtVfxInstance` on the avatar root after stock UniVRM load.
- `VrmxtVfxParticleSystemMapper` maps portable fields onto Unity `ParticleSystem` (billboard + local +Y velocity).
- Field table: [vfx-particle-mapping.md](vfx-particle-mapping.md).

Runtime attach after UniVRM load (caller owns UniGLTF/VRM references):

```csharp
using var data = new GlbLowLevelParser(path, File.ReadAllBytes(path)).Parse();
var vrm = await Vrm10.LoadGltfDataAsync(data);
var nodes = vrm.GetComponent<RuntimeGltfInstance>().Nodes;

// Missing extension → false (no-op). Unresolved nodes skip that emitter only.
if (VrmxtVfxRuntime.TryAttach(vrm.gameObject, data.Json, nodes, out var vfx))
{
    // Optional: build ParticleSystem children (null texture → solid tint fallback)
    vfx.BuildParticleSystems(index => ResolveGltfTexture(index));
}

// Or attach + build in one call:
VrmxtVfxRuntime.TryAttach(
    vrm.gameObject,
    data.Json,
    nodes,
    index => ResolveGltfTexture(index),
    out vfx);
```

### VRMXT_materials_override

- Per-material extension: `materials[i].extensions.VRMXT_materials_override`
- Spec: [vrmxt-materials-override.md](https://github.com/miramocha/Extended-VRM-Specs/blob/main/specs/vrmxt-materials-override.md)
- `VrmxtMaterialsOverride.TryParse` enforces unique `engine` values per material.
- `UnityOverrideSelector` matches `engine: unity` entries against the active render pipeline variant.
- Full `IMaterialDescriptorGenerator` wrapping requires UniVRM at consumption time; see `VrmxtMaterialsOverrideGenerator`.
- Host without generator inject (e.g. Warudo Character load): post-load re-read of `.vrm` JSON + material swap. Notes: [Warudo Materials Override](https://github.com/miramocha/Extended-VRM-Specs/blob/main/implementations/warudo-materials-override.md).

## UniVRM integration

- **VFX:** parse + `TryAttach` after `Vrm10.LoadGltfDataAsync` as above. UniVRMXT Runtime asmdef does **not** hard-reference UniGLTF/VRM10; the load caller supplies JSON, `Nodes`, and optional texture resolution.
- **Materials (planned):** wrap `IMaterialDescriptorGenerator` through `Vrm10.LoadPathAsync`; editor factory via project settings.
- **Editor `.vrm` import:** UniVRM `VrmScriptedImporter` has no post-extension callback; AssetDatabase path is tracked separately (issue #4).

## CI

`.github/workflows/validate.yml` runs `tools/validate_package.py`, which checks package metadata, asmdef presence, and `.meta` GUID uniqueness without launching Unity.
