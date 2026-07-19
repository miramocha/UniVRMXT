# Architecture

UniVRMXT is an optional consumer package for [Extended VRM](https://github.com/miramocha/Extended-VRM-Specs) glTF extensions. Normative behavior is defined in that repository; this package implements Unity-side parsing and integration hooks.

## Layering

| Layer | Location | Depends on |
|-------|----------|------------|
| Format | `Runtime/Format/` | `Newtonsoft.Json` only |
| VFX runtime | `Runtime/Vfx/` | Format, UnityEngine |
| Materials override | `Runtime/MaterialsOverride/` | Format, UnityEngine |
| Editor integration | `Editor/` | Runtime |
| Tests | `Tests/Format/`, `Tests/Vfx/`, `Tests/MaterialsOverride/` | Runtime (Editor, NUnit) |

The Format layer parses extension JSON with Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`)
and does not reference UniGLTF types, so format tests stay free of UniVRM load APIs.

## Extensions (v0.1.0)

### VRMXT_vfx

- Root extension: `extensions.VRMXT_vfx`
- Spec: [vrmxt-vfx.md](https://github.com/miramocha/Extended-VRM-Specs/blob/main/specs/vrmxt-vfx.md)
- `VrmxtVfx.TryParse` validates `specVersion` `1.0`, applies particle defaults, and skips invalid emitters.
- `VrmxtVfxImporter.TryImport` maps emitters to Unity data; Transform overloads skip unresolved nodes.
- `VrmxtVfxRuntime.TryAttach` adds `VrmxtVfxInstance` on the avatar root after stock UniVRM load (also wires `VrmxtInstance.Vfx`).
- Editing `VrmxtVfxInstance` emitter fields refreshes bound preview `ParticleSystem`s via `OnValidate` → `SyncParticleSystemsFromEmitters` (no rebuild).
- `VrmxtVfxParticleSystemMapper` maps portable fields onto Unity `ParticleSystem` (billboard + local +Y velocity; BIRP/URP unlit material).
- `VrmxtVfx.ToJson` / `VrmxtVfxExporter` + `VrmxtVfxExportHookBootstrap` re-write `VRMXT_vfx` on Extended-UniVRM VRM export.
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
- `VrmxtMaterialsOverride.TryParse` / `ToJson` / `ToUtf8Json` — full round-trip: selection-key uniqueness (`engine`, or `engine` + `material.variant` for Unity/Unreal), Unity `idType: shaderName` with multi-slot `builtin`/`urp`/`hdrp` siblings, Unreal `idType: resourcePath`, `properties[]` (`scalar` / `vector` / `texture` / `shaderFeature`), `bindings[]` sourced from a sibling `VRMC_materials_mtoon`.
- `UnityOverrideSelector` picks among `engine: unity` entries by active render pipeline: exact `variant` match, else exactly one empty/omitted variant, else stock import.
- `VrmxtInstance` — avatar-root facade with `Vfx` + `MaterialsOverride` component props; attach/export prefer facade then fall back to direct feature lookup.
- `VrmxtMaterialsOverrideInstance` — `MonoBehaviour` holding per-material pairs (`MaterialName`, read-only `SourceMaterial`, authoring `OverrideMaterial`, verbatim `ExtensionJson` for all engines) plus `ImportedTextures` (glTF texture indices decoded on import for export remapping) so apply and export stay round-trip safe. Inspector CustomEditor locks the VRM/glTF side; `OnValidate` syncs override Material → JSON + live renderers.
- `VrmxtMaterialsOverrideAuthoring` — upsert active `(unity, variant)` slot from a Material; sibling pipeline slots and other engines survive sync/re-export.
- `VrmxtMaterialsOverrideRuntime.TryAttachFromGltfJson` — walks `materials[]`, validates each extension through the Format layer, and populates the instance (wires `VrmxtInstance`). No UniGLTF/VRM10 reference, same design as the VFX runtime attach.
- `VrmxtMaterialsOverrideApplier.Apply` — shared logic for Editor and Warudo-style hosts: resolves the selected `unity` override per material, sets `shader`, then writes that slot's `properties` and `bindings` (bindings win on overlap, per base-spec rule 23). Bindings apply only when a sibling `VRMC_materials_mtoon` extension exists; an unresolved shader or a missing/mismatched variant leaves that material on stock import untouched.
- `VrmxtMaterialsOverrideExporter` — `BuildPending` clones each stored entry for export; `PrepareTextures` re-registers textures for every unity slot (selector-chosen slot prefers live OverrideMaterial / mesh textures; all slots fall back to `VrmxtMaterialsOverrideInstance.ImportedTextures` decoded on import — never write-through stale glTF indices); `TryBuildUtf8Extension` / `BuildAllUtf8Extensions` produce per-material UTF-8 JSON for the `WriteExtensions` phase, written via `Vrm10ExportExtensionContext.AddMaterialExtension` (material index resolved with `TryGetMaterialIndex`). `ResolveUnityVariant` implements variant survival: an existing `material.variant` always wins; only a brand-new `unity` entry without one is filled from the active pipeline.
- Host integration uses the same soft-detected `Vrm10Import/ExportExtensionRegistry` design as `VRMXT_vfx` (Editor + Extended-UniVRM) or a direct post-load JSON re-read for hosts without generator inject (e.g. Warudo Character load): [Warudo Materials Override](https://github.com/miramocha/Extended-VRM-Specs/blob/main/implementations/warudo-materials-override.md). Per-material `AddMaterialExtension` / `TryGetMaterialIndex` design notes: [univrm-upstream-hooks.md](https://github.com/miramocha/Extended-VRM-Specs/blob/main/implementations/univrm-upstream-hooks.md).
- Full `IMaterialDescriptorGenerator` wrapping (Editor import-time shader swap ahead of first render) still requires UniVRM at consumption time; see `VrmxtMaterialsOverrideGenerator` and `Editor/MaterialsOverride/VrmxtMaterialDescriptorGeneratorFactory.cs`.
- Unity↔Blender round-trip of this extension is blocked until VRMXT-Extension-for-Blender switches from `kind`/`name` to `idType`/`id` (replace, no dual-read). Tracked in [Blender Materials Override](https://github.com/miramocha/Extended-VRM-Specs/blob/main/implementations/blender-materials-override.md).

## UniVRM integration

- **VFX (runtime):** parse + `TryAttach` after `Vrm10.LoadGltfDataAsync` as above. UniVRMXT Runtime asmdef does **not** hard-reference UniGLTF/VRM10; the load caller supplies JSON, `Nodes`, and optional texture resolution.
- **VFX (AssetDatabase):** Dual path depending on host UniVRM:
  - **Extended-UniVRM** (hooks present + Project Settings/VRM10 → Enable VRM Import Extensions):
    `VrmxtVfxImportHookBootstrap` soft-detects `Vrm10ImportExtensionRegistry.IsEnabled` and
    attaches VFX onto the **original** `.vrm` during `VrmScriptedImporter` (no companion prefab).
  - **Stock UniVRM**, or Extended with import extensions **disabled**: `VrmxtVfxAssetPostprocessor`
    writes sibling **`*.vrmxt.prefab`** via `TryAttachFromGlb` (name-based node resolve + second-read textures).
  - Detection: registry type in `VRM10.Editor` plus `IsEnabled` (project setting); no hard `VRM10.Editor` asmdef reference.
  - **Export (Extended-UniVRM):** `VrmxtVfxExportHookBootstrap` soft-detects Runtime
    `Vrm10ExportExtensionRegistry` and writes `VRMXT_vfx` from `VrmxtVfxInstance`
    (Project Settings → Enable VRM Export Extensions).
  - Runtime hosts (Warudo, viewers): stock load, then `TryAttachFromGlb` (unchanged).
  - Design notes: [univrm-upstream-hooks.md](https://github.com/miramocha/Extended-VRM-Specs/blob/main/implementations/univrm-upstream-hooks.md).
- **Materials:** `VrmxtMaterialsOverrideRuntime.TryAttachFromGltfJson` + `VrmxtMaterialsOverrideApplier.Apply` run against any host's post-load `.vrm` JSON (Editor or Warudo-style runtime), same soft-detect pattern as VFX. Editor import hook second-reads the GLB (via `VrmxtVfxGlbTextures`) to resolve `properties[].texture` and persist `ImportedTextures` as sub-assets. Export: `VrmxtMaterialsOverrideExporter` feeds `Vrm10ExportExtensionContext.AddMaterialExtension` during `PrepareTextures` / `WriteExtensions` (per-material extension write; see [univrm-upstream-hooks.md](https://github.com/miramocha/Extended-VRM-Specs/blob/main/implementations/univrm-upstream-hooks.md)). Editor import-time `IMaterialDescriptorGenerator` wrapping (shader swap ahead of first render, via project settings factory) remains planned.

## CI

`.github/workflows/validate.yml` runs `tools/validate_package.py`, which checks package metadata, asmdef presence, and `.meta` GUID uniqueness without launching Unity.
