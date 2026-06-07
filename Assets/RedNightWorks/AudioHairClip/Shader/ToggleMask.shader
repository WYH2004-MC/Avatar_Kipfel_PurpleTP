Shader "RedNightWorks/AudioHairClip/ToggleMask"
{
    Properties
    {
        [NoScaleOffset]_MainTex ("Texture", 2D) = "white" {}
        [Toggle]
        _Toggle ("Toggle", int) = 0
        [NoScaleOffset]_Mask1 ("Mask1", 2D) = "white" {}
        [NoScaleOffset]_Mask2 ("Mask2", 2D) = "white" {}
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
            int _Toggle;
            sampler2D _Mask1;
            sampler2D _Mask2;
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

                fixed4 mask = tex2D(_Mask1, i.uv);
                mask *= 1-_Toggle;
                mask += tex2D(_Mask2, i.uv) * _Toggle;

                fixed4 color = _Color1;
                color *= 1-_Toggle;
                color += _Color2 * _Toggle;

                col.a = min(col.a, mask.r);
                col.rgb = mask.rgb * color.rgb;

                col *= _Emissive;
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
