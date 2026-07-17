# Architecture

UniVRMXT is an optional consumer package for [Extended VRM](https://github.com/miramocha/Extended-VRM-Specs) glTF extensions. Normative behavior is defined in that repository; this package implements Unity-side parsing and integration hooks.

## Layering

| Layer | Location | Depends on |
|-------|----------|------------|
| Format | `Runtime/Format/` | `System.Text.Json` only |
| VFX runtime | `Runtime/Vfx/` | Format, UnityEngine |
| Materials override | `Runtime/MaterialsOverride/` | Format, UnityEngine |
| Editor integration | `Editor/` | Runtime |
| Tests | `Tests/Format/` | Runtime (Editor, NUnit) |

The Format layer parses extension JSON without referencing UniGLTF types so CI and unit tests stay lightweight.

## Extensions (v0.1.0)

### VRMXT_vfx

- Root extension: `extensions.VRMXT_vfx`
- Spec: [vrmxt-vfx.md](https://github.com/miramocha/Extended-VRM-Specs/blob/main/specs/vrmxt-vfx.md)
- `VrmxtVfx.TryParse` validates `specVersion` `1.0`, applies particle defaults, and skips invalid emitters.
- `VrmxtVfxImporter.TryImport` resolves glTF node indices through a caller-supplied delegate.

### VRMXT_materials_override

- Per-material extension: `materials[i].extensions.VRMXT_materials_override`
- Spec: [vrmxt-materials-override.md](https://github.com/miramocha/Extended-VRM-Specs/blob/main/specs/vrmxt-materials-override.md)
- `VrmxtMaterialsOverride.TryParse` enforces unique `engine` values per material.
- `UnityOverrideSelector` matches `engine: unity` entries against the active render pipeline variant.
- Full `IMaterialDescriptorGenerator` wrapping requires UniVRM at consumption time; see `VrmxtMaterialsOverrideGenerator`.

## UniVRM integration (planned consumption)

When installed beside UniVRM 0.131.1:

1. Runtime VRM loads pass a wrapped `IMaterialDescriptorGenerator` through `Vrm10.LoadPathAsync` (or equivalent).
2. Editor import assigns a `MaterialDescriptorGeneratorFactory` subclass via project settings.

The v0.1.0 scaffold documents these integration points without hard asmdef references to UniGLTF.

## CI

`.github/workflows/validate.yml` runs `tools/validate_package.py`, which checks package metadata, asmdef presence, and `.meta` GUID uniqueness without launching Unity.
