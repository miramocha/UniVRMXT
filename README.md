# UniVRMXT

Optional Unity package for [Extended VRM](https://github.com/miramocha/Extended-VRM-Specs) glTF extensions on top of [UniVRM](https://github.com/vrm-c/UniVRM).

Version `0.1.0` provides foundation parsers and VFX runtime hooks for:

- `VRMXT_sprite_particle` — parse flat emitters, resolve glTF nodes after UniVRM load, store on `VrmxtVfxInstance`, optional `ParticleSystem` mapping
- `VRMXT_materials_override` — per-material engine override metadata
- `VRMXT_materials_mtoonxt` — legacy per-material stencil extras plus portable root-level stencil relationships; package ships `VRMXT/MToonXT10` (Built-in) and `VRMXT/Universal Render Pipeline/MToonXT10`

See [docs/architecture.md](docs/architecture.md) for runtime attach + AssetDatabase dual path,
[docs/mtoonxt-stencil.md](docs/mtoonxt-stencil.md) for the
portable relationship graph and Unity pass compiler,
(Extended-UniVRM import hooks gated by Project Settings/VRM10 vs stock companion prefab),
[docs/vfx-particle-mapping.md](docs/vfx-particle-mapping.md) for the ParticleSystem field table, and
[Extended-VRM-Specs univrm-upstream-hooks](https://github.com/miramocha/Extended-VRM-Specs/blob/main/implementations/univrm-upstream-hooks.md).

## Requirements

- Unity 2022.3 LTS or later
- `com.vrmc.gltf` and `com.vrmc.vrm` 0.131.2 (declared in `package.json`)

## Installation

See [docs/installation.md](docs/installation.md). Git UPM, Unity 2022.3 LTS or later:

1. Extended UniVRM (`?path=/Packages/UniGLTF` then `?path=/Packages/VRM10`).
2. This fork of UniVRMXT `https://github.com/tdw46/UniVRMXT.git`.

## Architecture

See [docs/architecture.md](docs/architecture.md).

## Materials override integration

The Runtime foundation compiles without hard references to UniGLTF assemblies so format
parsing and tests stay lightweight. Full `IMaterialDescriptorGenerator` wrapping lands
when this package is consumed inside a project with UniVRM restored. See
`VrmxtMaterialsOverrideGenerator` and `Editor/MaterialsOverride/VrmxtMaterialDescriptorGeneratorFactory.cs`.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
