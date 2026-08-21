Shader "VRMXT/Universal Render Pipeline/MToonXT10"
{
    Properties
    {
        // Rendering
        _AlphaMode ("alphaMode", Int) = 0
        _TransparentWithZWrite ("mtoon.transparentWithZWrite", Int) = 0
        _Cutoff ("alphaCutoff", Range(0, 1)) = 0.5 // Unity specified name
        _RenderQueueOffset ("mtoon.renderQueueOffsetNumber", Int) = 0
        _DoubleSided ("doubleSided", Int) = 0

        // Lighting
        _Color ("pbrMetallicRoughness.baseColorFactor", Color) = (1, 1, 1, 1) // Unity specified name
        _MainTex ("pbrMetallicRoughness.baseColorTexture", 2D) = "white" {} // Unity specified name
        _ShadeColor ("mtoon.shadeColorFactor", Color) = (1, 1, 1, 1)
        _ShadeTex ("mtoon.shadeMultiplyTexture", 2D) = "white" {}
        [Normal] _BumpMap ("normalTexture", 2D) = "bump" {} // Unity specified name
        _BumpScale ("normalTexture.scale", Float) = 1.0 // Unity specified name
        _ShadingShiftFactor ("mtoon.shadingShiftFactor", Range(-1, 1)) = -0.05
        _ShadingShiftTex ("mtoon.shadingShiftTexture", 2D) = "black" {} // channel R
        _ShadingShiftTexScale ("mtoon.shadingShiftTexture.scale", Float) = 1
        _ShadingToonyFactor ("mtoon.shadingToonyFactor", Range(0, 1)) = 0.95

        // GI
        _GiEqualization ("mtoon.giEqualizationFactor", Range(0, 1)) = 0.9

        // Emission
        [HDR] _EmissionColor ("emissiveFactor", Color) = (0, 0, 0, 1) // Unity specified name
        _EmissionMap ("emissiveTexture", 2D) = "white" {} // Unity specified name

        // Rim Lighting
        _MatcapColor ("mtoon.matcapFactor", Color) = (0, 0, 0, 1) // 仕様のデフォルト値は白だが、過去の仕様違反 UniVRM 実装アプリケーションのために黒とする。 https://github.com/vrm-c/UniVRM/pull/2594
        _MatcapTex ("mtoon.matcapTexture", 2D) = "black" {}
        _RimColor ("mtoon.parametricRimColorFactor", Color) = (0, 0, 0, 1)
        _RimFresnelPower ("mtoon.parametricRimFresnelPowerFactor", Range(0, 100)) = 5.0
        _RimLift ("mtoon.parametricRimLiftFactor", Range(0, 1)) = 0
        _RimTex ("mtoon.rimMultiplyTexture", 2D) = "white" {}
        _RimLightingMix ("mtoon.rimLightingMixFactor", Range(0, 1)) = 1

        // Outline
        _OutlineWidthMode ("mtoon.outlineWidthMode", Int) = 0
        [PowerSlider(2.2)] _OutlineWidth ("mtoon.outlineWidthFactor", Range(0, 0.05)) = 0
        _OutlineWidthTex ("mtoon.outlineWidthMultiplyTexture", 2D) = "white" {} // channel G
        _OutlineColor ("mtoon.outlineColorFactor", Color) = (0, 0, 0, 1)
        _OutlineLightingMix ("mtoon.outlineLightingMixFactor", Range(0, 1)) = 1

        // UV Animation
        _UvAnimMaskTex ("mtoon.uvAnimationMaskTexture", 2D) = "white" {} // channel B
        _UvAnimScrollXSpeed ("mtoon.uvAnimationScrollXSpeedFactor", Float) = 0
        _UvAnimScrollYSpeed ("mtoon.uvAnimationScrollYSpeedFactor", Float) = 0
        _UvAnimRotationSpeed ("mtoon.uvAnimationRotationSpeedFactor", Float) = 0

        // Unity ShaderPass Mode
        _M_CullMode ("_CullMode", Float) = 2.0
        _M_SrcBlend ("_SrcBlend", Float) = 1.0
        _M_DstBlend ("_DstBlend", Float) = 0.0
        _M_ZWrite ("_ZWrite", Float) = 1.0
        [Enum(Never,1,Less,2,Equal,3,LessEqual,4,Greater,5,NotEqual,6,GreaterEqual,7,Always,8)] _M_ZTest ("ZTest", Float) = 4
        _M_AlphaToMask ("_AlphaToMask", Float) = 0.0

        // Body / forward stencil (VRMC_materials_mtoonxt). Off until Enable stencil.
        [ToggleUI] _M_StencilEnabled ("Enable stencil", Float) = 0
        _M_StencilRef ("Stencil Ref", Range(0, 255)) = 0
        _M_StencilReadMask ("Stencil ReadMask", Range(0, 255)) = 255
        _M_StencilWriteMask ("Stencil WriteMask", Range(0, 255)) = 255
        [Enum(Never,1,Less,2,Equal,3,LessEqual,4,Greater,5,NotEqual,6,GreaterEqual,7,Always,8)] _M_StencilComp ("Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _M_StencilPass ("Stencil Pass", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _M_StencilFail ("Stencil Fail", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _M_StencilZFail ("Stencil ZFail", Float) = 0

        // Outline-pass stencil
        [ToggleUI] _M_OutlineStencilEnabled ("Enable outline stencil", Float) = 0
        _M_OutlineStencilRef ("Outline Stencil Ref", Range(0, 255)) = 0
        _M_OutlineStencilReadMask ("Outline Stencil ReadMask", Range(0, 255)) = 255
        _M_OutlineStencilWriteMask ("Outline Stencil WriteMask", Range(0, 255)) = 255
        [Enum(Never,1,Less,2,Equal,3,LessEqual,4,Greater,5,NotEqual,6,GreaterEqual,7,Always,8)] _M_OutlineStencilComp ("Outline Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _M_OutlineStencilPass ("Outline Stencil Pass", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _M_OutlineStencilFail ("Outline Stencil Fail", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _M_OutlineStencilZFail ("Outline Stencil ZFail", Float) = 0

        // etc
        _M_DebugMode ("_DebugMode", Float) = 0.0

        // for Editor
        _M_EditMode ("_EditMode", Float) = 0.0
    }

    // Shader Model 3.0
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }

        // Universal Forward Pass
        Pass
        {
            PackageRequirements
            {
                "unity": "2021.3"
                "com.unity.render-pipelines.universal": "12.0.0"
            }

            Name "MToonForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_M_CullMode]
            Blend [_M_SrcBlend] [_M_DstBlend]
            ZWrite [_M_ZWrite]
            ZTest [_M_ZTest]
            BlendOp Add, Max
            AlphaToMask [_M_AlphaToMask]

            Stencil
            {
                Ref [_M_StencilRef]
                ReadMask [_M_StencilReadMask]
                WriteMask [_M_StencilWriteMask]
                Comp [_M_StencilComp]
                Pass [_M_StencilPass]
                Fail [_M_StencilFail]
                ZFail [_M_StencilZFail]
            }

            HLSLPROGRAM
            #pragma target 3.0

            // Unity defined keywords
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma multi_compile __ _ALPHATEST_ON _ALPHABLEND_ON
            #pragma multi_compile __ _NORMALMAP
            #pragma multi_compile __ _MTOON_EMISSIVEMAP
            #pragma multi_compile __ _MTOON_RIMMAP
            #pragma multi_compile __ _MTOON_PARAMETERMAP
            #pragma shader_feature_local _ _MTOONXT_OVERLAY_DEPTH

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            #pragma vertex MToonVertex
            #pragma fragment MToonFragment

            #define MTOON_URP

            #include "./vrmc_materials_mtoon_forward_vertex.hlsl"
            #include "./vrmc_materials_mtoon_forward_fragment.hlsl"
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "unity": "2021.3"
                "com.unity.render-pipelines.universal": "12.0.0"
            }

            Name "UniversalForwardOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_M_CullMode]
            Blend [_M_SrcBlend] [_M_DstBlend]
            ZWrite Off
            ZTest Always
            BlendOp Add, Max
            AlphaToMask [_M_AlphaToMask]

            Stencil
            {
                Ref [_M_StencilRef]
                ReadMask [_M_StencilReadMask]
                WriteMask [_M_StencilWriteMask]
                Comp [_M_StencilComp]
                Pass [_M_StencilPass]
                Fail [_M_StencilFail]
                ZFail [_M_StencilZFail]
            }

            HLSLPROGRAM
            #pragma target 3.0

            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma multi_compile __ _ALPHATEST_ON _ALPHABLEND_ON
            #pragma multi_compile __ _NORMALMAP
            #pragma multi_compile __ _MTOON_EMISSIVEMAP
            #pragma multi_compile __ _MTOON_RIMMAP
            #pragma multi_compile __ _MTOON_PARAMETERMAP
            #pragma shader_feature_local _ _MTOONXT_OVERLAY_DEPTH

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            #pragma vertex MToonVertex
            #pragma fragment MToonFragment

            #define MTOON_URP
            #define MTOONXT_OVERLAY_DEPTH_PASS

            #include "./vrmc_materials_mtoon_forward_vertex.hlsl"
            #include "./vrmc_materials_mtoon_forward_fragment.hlsl"
            ENDHLSL
        }

        // MToon Outline Pass
        Pass
        {
            PackageRequirements
            {
                "unity": "2021.3"
                "com.unity.render-pipelines.universal": "12.0.0"
            }

            Name "MToonOutlineMain"
            Tags { "LightMode" = "MToonOutline" }

            Cull Front
            Blend [_M_SrcBlend] [_M_DstBlend]
            ZWrite [_M_ZWrite]
            ZTest [_M_ZTest]
            Offset 1, 1
            BlendOp Add, Max
            AlphaToMask [_M_AlphaToMask]

            Stencil
            {
                Ref [_M_OutlineStencilRef]
                ReadMask [_M_OutlineStencilReadMask]
                WriteMask [_M_OutlineStencilWriteMask]
                Comp [_M_OutlineStencilComp]
                Pass [_M_OutlineStencilPass]
                Fail [_M_OutlineStencilFail]
                ZFail [_M_OutlineStencilZFail]
            }

            HLSLPROGRAM
            #pragma target 3.0

            // Unity defined keywords
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma multi_compile __ _ALPHATEST_ON _ALPHABLEND_ON
            #pragma multi_compile __ _NORMALMAP
            #pragma multi_compile __ _MTOON_EMISSIVEMAP
            #pragma multi_compile __ _MTOON_RIMMAP
            #pragma multi_compile __ _MTOON_PARAMETERMAP
            #pragma multi_compile __ _MTOON_OUTLINE_WORLD _MTOON_OUTLINE_SCREEN
            #pragma shader_feature_local _ _MTOONXT_OUTLINE_OVERLAY_DEPTH

            #pragma vertex MToonVertex
            #pragma fragment MToonFragment

            #define MTOON_URP
            #define MTOON_PASS_OUTLINE

            #include "./vrmc_materials_mtoon_forward_vertex.hlsl"
            #include "./vrmc_materials_mtoon_forward_fragment.hlsl"
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "unity": "2021.3"
                "com.unity.render-pipelines.universal": "12.0.0"
            }

            Name "MToonOutlineOverlay"
            Tags { "LightMode" = "MToonOutline" }

            Cull Front
            Blend [_M_SrcBlend] [_M_DstBlend]
            ZWrite Off
            ZTest Always
            Offset 1, 1
            BlendOp Add, Max
            AlphaToMask [_M_AlphaToMask]

            Stencil
            {
                Ref [_M_OutlineStencilRef]
                ReadMask [_M_OutlineStencilReadMask]
                WriteMask [_M_OutlineStencilWriteMask]
                Comp [_M_OutlineStencilComp]
                Pass [_M_OutlineStencilPass]
                Fail [_M_OutlineStencilFail]
                ZFail [_M_OutlineStencilZFail]
            }

            HLSLPROGRAM
            #pragma target 3.0

            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma multi_compile __ _ALPHATEST_ON _ALPHABLEND_ON
            #pragma multi_compile __ _NORMALMAP
            #pragma multi_compile __ _MTOON_EMISSIVEMAP
            #pragma multi_compile __ _MTOON_RIMMAP
            #pragma multi_compile __ _MTOON_PARAMETERMAP
            #pragma multi_compile __ _MTOON_OUTLINE_WORLD _MTOON_OUTLINE_SCREEN
            #pragma shader_feature_local _ _MTOONXT_OUTLINE_OVERLAY_DEPTH

            #pragma vertex MToonVertex
            #pragma fragment MToonFragment

            #define MTOON_URP
            #define MTOON_PASS_OUTLINE
            #define MTOONXT_OVERLAY_DEPTH_PASS

            #include "./vrmc_materials_mtoon_forward_vertex.hlsl"
            #include "./vrmc_materials_mtoon_forward_fragment.hlsl"
            ENDHLSL
        }

        //  Depth Only Pass
        Pass
        {
            PackageRequirements
            {
                "unity": "2021.3"
                "com.unity.render-pipelines.universal": "12.0.0"
            }

            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull [_M_CullMode]
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0

            // Unity defined keywords
            #pragma multi_compile_instancing

            #pragma multi_compile __ _ALPHATEST_ON _ALPHABLEND_ON

            #pragma vertex MToonDepthOnlyVertex
            #pragma fragment MToonDepthOnlyFragment

            #define MTOON_URP

            #include "./vrmc_materials_mtoon_depthonly_vertex.hlsl"
            #include "./vrmc_materials_mtoon_depthonly_fragment.hlsl"
            ENDHLSL
        }

        //  Depth Normals Pass
        Pass
        {
            PackageRequirements
            {
                "unity": "2021.3"
                "com.unity.render-pipelines.universal": "12.0.0"
            }

            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            Cull [_M_CullMode]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0

            // Unity defined keywords
            #pragma multi_compile_instancing

            #pragma multi_compile __ _ALPHATEST_ON _ALPHABLEND_ON
            #pragma multi_compile __ _NORMALMAP

            #pragma vertex MToonDepthNormalsVertex
            #pragma fragment MToonDepthNormalsFragment

            #define MTOON_URP

            #include "./vrmc_materials_mtoon_depthnormals_vertex.hlsl"
            #include "./vrmc_materials_mtoon_depthnormals_fragment.hlsl"
            ENDHLSL
        }

        //  Shadow Caster Pass
        Pass
        {
            PackageRequirements
            {
                "unity": "2021.3"
                "com.unity.render-pipelines.universal": "12.0.0"
            }

            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull [_M_CullMode]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0

            // Unity defined keywords
            #pragma multi_compile_instancing

            #pragma multi_compile __ _ALPHATEST_ON _ALPHABLEND_ON

            #pragma vertex MToonShadowCasterVertex
            #pragma fragment MToonShadowCasterFragment

            #define MTOON_URP

            #include "./vrmc_materials_mtoon_shadowcaster_vertex.hlsl"
            #include "./vrmc_materials_mtoon_shadowcaster_fragment.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "UniVRMXT.Editor.Mtoonxt.MtoonxtInspector"
}
