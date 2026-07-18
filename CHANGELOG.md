# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- `VrmxtVfxInstance` runtime component and `VrmxtVfxRuntime.TryAttach` for post-load VFX data
- `VrmxtVfxImporter` Transform / node-list overloads that skip unresolved emitters
- `VrmxtVfxParticleSystemMapper` — portable particle → Unity `ParticleSystem` (local +Y velocity, billboard, BIRP/URP material + `_MainTex`/`_BaseMap` texture / solid-tint fallback)
- `VrmxtVfxRuntime.TryAttach` overloads that build `ParticleSystem` children via texture resolver
- `GlbChunks` / `GltfImageBytes` / `VrmxtVfxGlbTextures` — second GLB read for VFX-only textures UniVRM skips
- `VrmxtVfxRuntime.TryAttachFromGlb` for runtime / Warudo-style hosts
- `VrmxtVfxNodeResolver` for AssetDatabase node resolution without `RuntimeGltfInstance`
- `VrmxtVfxImportHookBootstrap` — soft-detect Extended-UniVRM import hooks (`IsEnabled` project setting); attach VFX on original `.vrm` when enabled
- `VrmxtVfxAssetPostprocessor` — companion `*.vrmxt.prefab` fallback for stock UniVRM or when import extensions are disabled
- Field mapping doc: `docs/vfx-particle-mapping.md`
- Upstream hook notes: [Extended-VRM-Specs univrm-upstream-hooks](https://github.com/miramocha/Extended-VRM-Specs/blob/main/implementations/univrm-upstream-hooks.md)
- VFX importer / attach / ParticleSystem / node-resolver NUnit tests under `Tests/Vfx/`

### Changed

- Particle materials: broader shader fallbacks + persist owned mats/`AddObjectToAsset` on import (avoids pink missing-shader particles)
- Format parsers use Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`) instead of
  System.Text.Json so they compile under Unity (STJ types are inaccessible there)
- Public `TryParse` overloads now take `JToken` instead of `JsonElement`
- Folder `.meta` files use Unity sibling layout (`FolderName.meta`); `generate_metas.py`
  preserves existing GUIDs

## [0.1.0] - 2026-07-17

### Added

- Initial UPM package scaffold (`com.miramocha.univrmxt`)
- `VRMXT_vfx` format parser with spec defaults and validation
- `VRMXT_materials_override` format parser with Unity and Unreal profiles
- `UnityOverrideSelector` for pipeline variant matching
- `VrmxtVfxData` / `VrmxtVfxImporter` runtime stubs
- `VrmxtMaterialsOverrideGenerator` stub and editor factory placeholder
- NUnit format tests and `tools/validate_package.py` CI check
