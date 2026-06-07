Shader "RedNightWorks/AudioHairClip/Volume"
{
    Properties
    {
        [NoScaleOffset]_MainTex ("Texture", 2D) = "white" {}
        _Volume ("Volume", Range(0, 1)) = 0.5
        _VolumeMultiplier ("Volume Multiplier", Float) = 1
        _Divide ("Divide", Float) = 10
        _Gap ("Gap", Range(0, 1)) = 0.1
        _Color1 ("Color1", Color) = (1,1,1,1)
        _Color2 ("Color2", Color) = (1,1,1,1)
        _Emissive ("Emissive", Range(0, 5)) = 1
    }
    SubShader
    {
        Tags{ "Queue"="AlphaTest" "RenderType"="TransparentCutout" "VRCFallback"="Hidden" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _Volume;
            float _VolumeMultiplier;
            float _Divide;
            float _Gap;
            fixed4 _Color1;
            fixed4 _Color2;
            float _Emissive;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                float volume = _Volume * _VolumeMultiplier;

                float pos = i.uv.x;
                fixed maskFloor = floor(pos * _Divide) / _Divide;
                fixed maskVolume = step(maskFloor+0.99/_Divide, volume);
                fixed maskFrac = frac(pos * _Divide) / _Divide;
                
                fixed4 col2;
                col2 = lerp(_Color1, _Color2, maskFloor) * step(_Gap, maskFrac);
                col2.a = maskVolume;
                col2.a *= step(1/(_Divide*2), volume);

                col.a = min(col.a, col2.a);
                col.rgb = col2.rgb;

                col *= _Emissive;
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
    
            ENDCG
        }
    }
}
