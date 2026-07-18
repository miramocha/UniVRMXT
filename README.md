# UniVRMXT

Optional Unity package for [Extended VRM](https://github.com/miramocha/Extended-VRM-Specs) glTF extensions on top of [UniVRM](https://github.com/vrm-c/UniVRM).

Version `0.1.0` provides foundation parsers and VFX runtime hooks for:

- `VRMXT_vfx` — parse emitters, resolve glTF nodes after UniVRM load, store on `VrmxtVfxInstance`, optional `ParticleSystem` mapping
- `VRMXT_materials_override` — per-material engine override metadata

See [docs/architecture.md](docs/architecture.md) for runtime attach + AssetDatabase import
(decision: companion `*.vrmxt.prefab` via AssetPostprocessor),
[docs/vfx-particle-mapping.md](docs/vfx-particle-mapping.md) for the ParticleSystem field table, and
[Extended-VRM-Specs univrm-upstream-hooks](https://github.com/miramocha/Extended-VRM-Specs/blob/main/implementations/univrm-upstream-hooks.md)
for ScriptedImporter limits and hooks to request from UniVRM.

## Requirements

- Unity 2022.3 LTS
- `com.vrmc.gltf` and `com.vrmc.vrm` 0.131.1 (declared in `package.json`)

## Installation

See [docs/installation.md](docs/installation.md).

## Architecture

See [docs/architecture.md](docs/architecture.md).

## Materials override integration

The Runtime foundation compiles without hard references to UniGLTF assemblies so format
parsing and tests stay lightweight. Full `IMaterialDescriptorGenerator` wrapping lands
when this package is consumed inside a project with UniVRM restored. See
`VrmxtMaterialsOverrideGenerator` and `Editor/MaterialsOverride/VrmxtMaterialDescriptorGeneratorFactory.cs`.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
