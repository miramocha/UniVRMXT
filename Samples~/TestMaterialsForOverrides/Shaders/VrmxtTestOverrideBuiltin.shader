Shader "VRMXT/Samples/TestOverrideBuiltin"
{
    Properties
    {
        // Visible unlit color — change in the Material inspector / properties[].
        _Color ("Main Color", Color) = (0, 1, 0, 1)

        // Binding targets (Unity profile / VRMC_materials_mtoon sources).
        _ShadeColor ("Shade Color", Color) = (0.8, 0.8, 0.8, 1)
        _ShadeTex ("Shade Multiply", 2D) = "white" {}
        _ShadingShiftFactor ("Shading Shift", Range(-1, 1)) = 0
        _ShadingShiftTex ("Shading Shift Map", 2D) = "black" {}
        _ShadingShiftTexScale ("Shading Shift Map Scale", Float) = 1
        _ShadingToonyFactor ("Shading Toony", Range(0, 1)) = 0.9
        _GiEqualizationFactor ("GI Equalization", Range(0, 1)) = 0.9

        // Unbound sample property + keyword for properties[].shaderFeature.
        _OutlineWidth ("Outline Width", Float) = 0.02
        [Toggle(_USE_RIM_LIGHT)] _UseRimLight ("Use Rim Light", Float) = 0
    }

    // Built-in RP test material for VRMXT_materials_override.
    // Declares override property names so Applier SetFloat/SetColor/SetTexture/keywords work.
    // Fragment outputs _Color (default green).
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = ""
        }

        Pass
        {
            Name "VRMXTTestOverrideBuiltinUnlit"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_fwdbase
            #pragma shader_feature_local _USE_RIM_LIGHT
            #include "UnityCG.cginc"

            float4 _Color;
            float4 _ShadeColor;
            sampler2D _ShadeTex;
            float4 _ShadeTex_ST;
            float _ShadingShiftFactor;
            sampler2D _ShadingShiftTex;
            float4 _ShadingShiftTex_ST;
            float _ShadingShiftTexScale;
            float _ShadingToonyFactor;
            float _GiEqualizationFactor;
            float _OutlineWidth;
            float _UseRimLight;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Keep binding targets live so Unity does not strip unused uniforms.
                float sink = _ShadeColor.r
                    + tex2D(_ShadeTex, i.uv).r
                    + _ShadingShiftFactor
                    + tex2D(_ShadingShiftTex, i.uv).r * _ShadingShiftTexScale
                    + _ShadingToonyFactor
                    + _GiEqualizationFactor
                    + _OutlineWidth
                    + _UseRimLight;
#if defined(_USE_RIM_LIGHT)
                sink += 0.0001;
#endif
                sink *= 0.0;

                return _Color + sink;
            }
            ENDCG
        }
    }

    FallBack Off
}
