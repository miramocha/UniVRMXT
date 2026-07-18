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
- `VrmxtVfxExportHookBootstrap` — soft-detect Extended-UniVRM export hooks; write `VRMXT_vfx` from `VrmxtVfxInstance` (strip preview ParticleSystems on export copy)
- `VrmxtVfx.ToJson` / `ToUtf8Json` and `VrmxtVfxExporter` for portable re-export
- Export sync: live `ParticleSystem` preview values (e.g. `startColor`) fold back into
  `VrmxtVfxInstance` before writing `VRMXT_vfx`
- Particle materials: force Transparent + Alpha blend so texture alpha works (URP Particles/Unlit
  defaults to Opaque when created from script)
- Bind/sync particle textures onto `VrmxtVfxInstance` emitters so re-export keeps albedo
  after preview ParticleSystems are cleared or only materials were persisted
- `VrmxtVfxAssetPostprocessor` — companion `*.vrmxt.prefab` fallback for stock UniVRM or when import extensions are disabled
- Field mapping doc: `docs/vfx-particle-mapping.md`
- Upstream hook notes: [Extended-VRM-Specs univrm-upstream-hooks](https://github.com/miramocha/Extended-VRM-Specs/blob/main/implementations/univrm-upstream-hooks.md)
- VFX importer / attach / ParticleSystem / node-resolver / exporter NUnit tests under `Tests/Vfx/` and `Tests/Format/`

### Changed

- `VrmxtVfxParticleSystemMapper.Apply`: call `Play(true)` after configure (`Stop` clears
  playOnAwake start)
- `ResolveParticleShader`: try host URP **and** BIRP names before packaged shader
  (pipeline null only sets search order — import-time null must not skip URP names);
  packaged `VRMXT/Particles Unlit` + Resources mat for build inclusion
- `PackagedMaterialProvider` / `PreferPackagedParticleMaterial` for hosts that load the
  packaged mat via ModHost (Warudo) instead of `Resources.Load`
- Ship first-party particle shader + `Resources/UniVRMXT/ParticlesUnlit` material so builds
  keep a usable particle shader without Always Included lists
- Particle materials: broader shader fallbacks; persist textures before materials and re-bind
  albedo slots on import (avoids pink / empty-texture particles)
- Import hooks: `ImportHooksAvailable` requires successful handler registration
- Companion prefab: delete on missing `VRMXT_vfx`, `.vrm` delete/move; shell-first persist
  so textures are sub-asseted before materials
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
