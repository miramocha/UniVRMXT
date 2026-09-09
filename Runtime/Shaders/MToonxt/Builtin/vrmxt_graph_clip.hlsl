#ifndef VRMXT_GRAPH_CLIP_INCLUDED
#define VRMXT_GRAPH_CLIP_INCLUDED
sampler2D _VRMXTGraphMask;
float _VRMXTGraphEnabled;
float4x4 _VRMXTGraphVP;

void VrmxtGraphClip(float3 positionWS)
{
    if (_VRMXTGraphEnabled < 0.5) return;
    float4 p = mul(_VRMXTGraphVP, float4(positionWS, 1));
    float2 uv = p.xy / p.w * 0.5 + 0.5;
#if UNITY_UV_STARTS_AT_TOP
    uv.y = 1 - uv.y;
#endif
    clip(0.5 - tex2D(_VRMXTGraphMask, uv).r);
}
#endif
