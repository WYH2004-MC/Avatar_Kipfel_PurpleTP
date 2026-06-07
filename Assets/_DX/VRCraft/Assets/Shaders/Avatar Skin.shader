Shader "DX/VRCraft/Avatar Skin"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        
		[HDR]_EmissionColor ("Color", Color) = (0,0,0,1)
		_EmissionMap ("Emission", 2D) = "white" {}

        _NormalSize ("Normal Size", Range(0,1)) = 0.065
        _NormalIntensity ("Normal Intensity", Range(-1,1)) = 0.17
        _Glossiness ("Smoothness", Range(0,1)) = 0.4
        _GlossinessIntensity ("Smoothness Intensity", Range(-1,1)) = 0.02
        _Metallic ("Metallic", Range(0,1)) = 0.0        
        [MaterialToggle] _HueShit ("Hue Shit", Float ) = 0
        
        [Header(FallBack)]        
        _Cutoff("Cutoff", Range(0,1)) = .9    
        _Mode ("Mode", Float) = 1.0
    }
    SubShader
    {
        Tags  {"Queue"="AlphaTest" "VRCFallback"="DoubleSidedCutout"  "IgnoreProjector"="True" "RenderType"="Transparent"  }
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 200
        CGINCLUDE        
            // Use shader model 3.0 target, to get nicer looking lighting
            #pragma target 3.0

            sampler2D _MainTex,_EmissionMap;
            float _NormalSize;
            float _NormalIntensity;
            float _GlossinessIntensity;

            half _Glossiness;
            half _Metallic;
            fixed4 _Color,_EmissionColor;
            float4 _MainTex_TexelSize;
            float _HueShit;

            struct Input
            {
                float2 uv_MainTex;
                float IsFacing:VFACE;
            };

            // #pragma instancing_options assumeuniformscaling
            UNITY_INSTANCING_BUFFER_START(Props)
                // put more per-instance properties here
            UNITY_INSTANCING_BUFFER_END(Props)

            // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
            // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.

            float toGray( float4 color ){
                return dot(color.rgb, float3(0.2126, 0.7152, 0.0722));//*color.a;
            }
            float3 rgb2hsv(float3 c) {
              float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
              float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
              float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));

              float d = q.x - min(q.w, q.y);
              float e = 1.0e-10;
              return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float3 hsv2rgb(float3 c) {
              c = float3(c.x, clamp(c.yz, 0.0, 1.0));
              float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
              float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
              return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
            }

            void surfBase (Input IN, inout SurfaceOutputStandard o)
            {
                fixed4 c = tex2Dlod (_MainTex, float4(IN.uv_MainTex,0,0)) * _Color;
                o.Albedo = c.rgb;
                if( _NormalSize  > 0 ){
                    float3 ts  = float3 (_MainTex_TexelSize.xy, 0)*_NormalSize;
                    float2 uv = IN.uv_MainTex-ts.xy/2.0;
                    float2 uv0 = uv + ts.xz;
                    float2 uv1 = uv + ts.zy;
                    float h =  toGray(tex2Dlod (_MainTex, float4(uv,0,0)));
                    float h0 =  toGray(tex2Dlod (_MainTex, float4(uv0,0,0)));
                    float h1 =  toGray(tex2Dlod (_MainTex, float4(uv1,0,0)));                
                    float3 p0 = float3 (ts.xz, h0 - h);
                    float3 p1 = float3 (ts.zy, h1 - h);
                    float3 normal = normalize (cross (p0, p1));
                    normal.xy*=sign(_NormalIntensity);
                    o.Normal =  normalize (lerp(float3(0,0,1),normal,abs(_NormalIntensity)));
                }    
                if(_HueShit == 1 ) o.Albedo.rgb=hsv2rgb(rgb2hsv(c.rgb)+float3(_Time.y*-.5,0,0));
                o.Normal*=IN.IsFacing;     
                o.Emission = tex2Dlod (_EmissionMap, float4(IN.uv_MainTex,0,0)).rgb * _EmissionColor;
                
                // Metallic and smoothness come from slider variables
                o.Metallic = _Metallic;
                o.Smoothness = saturate(_Glossiness+(toGray(c)-.5)*_GlossinessIntensity*2);
                o.Alpha = c.a;
            }


        ENDCG
        
        ZWrite On
        Cull Off
        CGPROGRAM
            #pragma surface surf Standard fullforwardshadows addshadow

            void surf (Input IN, inout SurfaceOutputStandard o)
            {
                surfBase(IN,o);
                if( o.Alpha < 1  ) discard; // if( o.Alpha < 1 && IN.uv_MainTex.x > 1 ) discard;
            }
        ENDCG
            
        ZWrite Off
        Cull Front
        CGPROGRAM
            #pragma surface surf Standard fullforwardshadows alpha:fade
                    
            void surf (Input IN, inout SurfaceOutputStandard o)
            {    
                surfBase(IN,o);
                if( o.Alpha == 0 || o.Alpha == 1 ) discard;            //if( IN.uv_MainTex.x < 1 ||  o.Alpha == 0 || o.Alpha == 1 ) discard;      
            }
        ENDCG

        ZWrite On
        Cull Back
        CGPROGRAM
            #pragma surface surf Standard fullforwardshadows alpha:fade

            void surf (Input IN, inout SurfaceOutputStandard o)
            {
                surfBase(IN,o);
                if(  o.Alpha == 0 || o.Alpha == 1 ) discard; // if( IN.uv_MainTex.x < 1 ||  o.Alpha == 0 || o.Alpha == 1 ) discard;
            }
        ENDCG

        

        
    }
    FallBack "Diffuse"
}
