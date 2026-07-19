# Test Materials for Overrides

Unlit test shaders for `VRMXT_materials_override`. Same property slots on both pipelines.
Fragment color is `tex2D(_MainTex) * _Color` — sample albedo × tint (Built-in green / URP yellow).

| Pipeline | Shader.Find name | Default `_Color` |
|----------|------------------|------------------|
| Built-in | `VRMXT/Samples/TestOverrideBuiltin` | green `(0,1,0,1)` |
| URP | `VRMXT/Samples/TestOverrideURP` | yellow `(1,1,0,1)` |

Sample albedo: `Textures/VrmxtTestTexture.png` (assigned on both Materials as `_MainTex`).

URP sample needs Universal RP in the project (`#include` of URP Core.hlsl).
The URP shader declares `PackageRequirements` inside its `SubShader` so Built-in-only
projects skip compiling it (avoids console errors after Test Runner / domain reload).

## Property ↔ binding map

| Material property | Typical `bindings[].source` | `targetType` |
|-------------------|----------------------------|--------------|
| `_ShadeColor` | `shadeColorFactor` | `vector` |
| `_ShadeTex` | `shadeMultiplyTexture` | `texture` |
| `_ShadingShiftFactor` | `shadingShiftFactor` | `scalar` |
| `_ShadingShiftTex` | `shadingShiftTexture` | `texture` |
| `_ShadingShiftTexScale` | `shadingShiftTexture.scale` | `scalar` |
| `_ShadingToonyFactor` | `shadingToonyFactor` | `scalar` |
| `_GiEqualizationFactor` | `giEqualizationFactor` | `scalar` |

Unbound samples for `properties[]`:

| Property | Type | Notes |
|----------|------|--------|
| `_MainTex` | `texture` | Albedo sample (material asset / export remaps index) |
| `_Color` | `vector` | Tint multiplied with `_MainTex` |
| `_OutlineWidth` | `scalar` | |
| `_USE_RIM_LIGHT` | `shaderFeature` | |

## Install

1. Package Manager → UniVRMXT → **Samples** → **Test Materials for Overrides**.
2. Match project RP to the shader you use (`variant: "builtin"` or `"urp"`).
3. Keep sample assets referenced (or Always Included Shaders) so `Shader.Find` works in builds.

## Example JSON

- `example-override-builtin.json` — Built-in override with green `_Color`
- `example-override-urp.json` — URP override with yellow `_Color`

Attach under `materials[i].extensions.VRMXT_materials_override` and list the extension in `extensionsUsed`.
