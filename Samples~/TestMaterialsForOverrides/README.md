# Test Materials for Overrides

Unlit test shaders for `VRMXT_materials_override`. Same property slots on both pipelines; solid colors show that apply succeeded.

| Pipeline | Shader.Find name | Color |
|----------|------------------|-------|
| Built-in | `VRMXT/Samples/TestOverrideBuiltin` | green |
| URP | `VRMXT/Samples/TestOverrideURP` | yellow |

URP sample needs Universal RP in the project (`#include` of URP Core.hlsl).

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

| Property | Type |
|----------|------|
| `_OutlineWidth` | `scalar` |
| `_USE_RIM_LIGHT` | `shaderFeature` |

## Install

1. Package Manager → UniVRMXT → **Samples** → **Test Materials for Overrides**.
2. Match project RP to the shader you use (`variant: "builtin"` or `"urp"`).
3. Keep sample assets referenced (or Always Included Shaders) so `Shader.Find` works in builds.

## Example JSON

- `example-override-builtin.json` — green Built-in override
- `example-override-urp.json` — yellow URP override

Attach under `materials[i].extensions.VRMXT_materials_override` and list the extension in `extensionsUsed`.
