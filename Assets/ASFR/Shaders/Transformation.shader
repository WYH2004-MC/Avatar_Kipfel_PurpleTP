Shader "ASFR/Transformation"
{
    Properties
    {
        _MainTex ("Original base texture", 2D) = "white" {}
        _Color ("Original tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.01
        _ASFRProgress ("Transformation", Range(0,1)) = 0
        _ASFRStyle ("0 Gold / 1 Stone / 2 Rubber", Float) = 0
        _Gold ("Gold", Color) = (1,0.66,0.16,1)
        _Stone ("Stone", Color) = (0.36,0.38,0.4,1)
        _Rubber ("Rubber", Color) = (0.025,0.025,0.03,1)
        _Edge ("Boundary softness", Range(0.001,0.1)) = 0.015
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color, _Gold, _Stone, _Rubber;
        float _Cutoff, _ASFRProgress, _ASFRStyle, _Edge;
        struct Input { float2 uv_MainTex; float height; float3 restPosition; };
        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.height = v.texcoord3.x;
            o.restPosition = v.vertex.xyz;
        }
        float grain(float3 p) { return frac(sin(dot(floor(p*340),float3(12.9898,78.233,37.719)))*43758.5453); }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 base = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            clip(base.a - _Cutoff);
            float threshold = lerp(-2*_Edge, 1+2*_Edge, saturate(_ASFRProgress));
            float amount = 1-smoothstep(threshold-_Edge, threshold+_Edge, IN.height);
            float3 color = _Gold.rgb;
            float metal = 1, smoothness = 0.88;
            if (_ASFRStyle > 0.5 && _ASFRStyle < 1.5)
            { color = _Stone.rgb * lerp(0.72,1.18,grain(IN.restPosition)); metal=0; smoothness=0.16; }
            if (_ASFRStyle >= 1.5) { color=_Rubber.rgb; metal=0; smoothness=0.72; }
            o.Albedo=lerp(base.rgb,color,amount);
            o.Metallic=metal*amount;
            o.Smoothness=lerp(0.25,smoothness,amount);
            o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Transparent/Cutout/Diffuse"
}
