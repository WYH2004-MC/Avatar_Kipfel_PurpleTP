Shader "RedNightWorks/AudioHairClip/Viseme"
{
    Properties
    {
        [NoScaleOffset]_MainTex ("Texture", 2D) = "white" {}
        _Viseme ("Viseme", Float) = 0
        _Color0 ("Color0", Color) = (1,1,1,1)
        _Color1 ("Color1", Color) = (1,1,1,1)
        _Color2 ("Color2", Color) = (1,1,1,1)
        _Color3 ("Color3", Color) = (1,1,1,1)
        _Color4 ("Color4", Color) = (1,1,1,1)
        _Color5 ("Color5", Color) = (1,1,1,1)
        _Color6 ("Color6", Color) = (1,1,1,1)
        _Color7 ("Color7", Color) = (1,1,1,1)
        _Color8 ("Color8", Color) = (1,1,1,1)
        _Color9 ("Color9", Color) = (1,1,1,1)
        _Color10 ("Color10", Color) = (1,1,1,1)
        _Color11 ("Color11", Color) = (1,1,1,1)
        _Color12 ("Color12", Color) = (1,1,1,1)
        _Color13 ("Color13", Color) = (1,1,1,1)
        _Color14 ("Color14", Color) = (1,1,1,1)
        _Color15 ("Color15", Color) = (0,0,0,0)
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
            Float _Viseme;
            fixed4 _Color0;
            fixed4 _Color1;
            fixed4 _Color2;
            fixed4 _Color3;
            fixed4 _Color4;
            fixed4 _Color5;
            fixed4 _Color6;
            fixed4 _Color7;
            fixed4 _Color8;
            fixed4 _Color9;
            fixed4 _Color10;
            fixed4 _Color11;
            fixed4 _Color12;
            fixed4 _Color13;
            fixed4 _Color14;
            fixed4 _Color15;
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
                fixed4 col2;
                col2 = lerp(_Color0, _Color1, step(1-0.5, _Viseme));
                col2 = lerp(col2, _Color2, step(2-0.5, _Viseme));
                col2 = lerp(col2, _Color3, step(3-0.5, _Viseme));
                col2 = lerp(col2, _Color4, step(4-0.5, _Viseme));
                col2 = lerp(col2, _Color5, step(5-0.5, _Viseme));
                col2 = lerp(col2, _Color6, step(6-0.5, _Viseme));
                col2 = lerp(col2, _Color7, step(7-0.5, _Viseme));
                col2 = lerp(col2, _Color8, step(8-0.5, _Viseme));
                col2 = lerp(col2, _Color9, step(9-0.5, _Viseme));
                col2 = lerp(col2, _Color10, step(10-0.5, _Viseme));
                col2 = lerp(col2, _Color11, step(11-0.5, _Viseme));
                col2 = lerp(col2, _Color12, step(12-0.5, _Viseme));
                col2 = lerp(col2, _Color13, step(13-0.5, _Viseme));
                col2 = lerp(col2, _Color14, step(14-0.5, _Viseme));
                col2 = lerp(col2, _Color15, step(15-0.5, _Viseme));

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
