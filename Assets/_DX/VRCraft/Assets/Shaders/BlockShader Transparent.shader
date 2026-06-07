Shader "DX/VRCraft/BlockShader Transparent"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        _EmissionColor("Color", Color) = (0,0,0)
		_EmissionMap("Emission", 2D) = "white" {}
        
        [MaterialToggle] _Animated ("Animated", Float ) = 0
        _GridX ("GridX", Float) = 1.0
        _GridY ("GridY", Float) = 1.0
        _FPS ("FPS", Float) = 24.0

        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Int) = 0
        
        [Header(FallBack)]        
        _Cutoff("Cutoff", Range(0,1)) = .5    
        _Mode ("Mode", Float) = 3
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType"="Transparent" "VRCFallback"="DoubleSidedToonTransparent"  }
        LOD 200
        ZWrite Off
        Cull [_CullMode]

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows decal:blend
        #pragma vertex vert
        #include "UnityCG.cginc"

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex,_EmissionMap;

        struct Input
        {
            float2 uv_MainTex;
            float3 vertexNormal; 
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color,_EmissionColor;
        float _GridX,_GridY,_FPS,_Animated;


        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void vert (inout appdata_full v, out Input o) {
           UNITY_INITIALIZE_OUTPUT(Input,o);
           o.vertexNormal = v.normal;
       }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            //_GridX,_GridY,_FPS,_Animated;
            // Albedo comes from a texture tinted by color
            float2 uv = IN.uv_MainTex;
            if(_Animated==1){
                float t = floor((_Time.y*_FPS)%(_GridX*_GridY));
                uv.y-=floor(t/_GridX);
                uv.x-=floor(t%_GridX);
                if(frac(uv.y)>.95) discard;
                uv/=float2(_GridX,_GridY);
            }
            float color= abs(IN.vertexNormal.x*.7)+abs(IN.vertexNormal.z*.4);
            if( IN.vertexNormal.y > 0 ){
                color+= abs( IN.vertexNormal.y*1);
            }else{
                color+= abs( IN.vertexNormal.y*.2);
            }
            
            fixed4 c = tex2Dlod (_MainTex, float4(uv,0,0)) * _Color;
            fixed4 e = tex2Dlod (_EmissionMap, float4(uv,0,0)) * (_EmissionColor+color/2);
            

            o.Albedo = c.rgb;
            o.Emission=e.rgb;
            /* 
            o.Albedo.rgb = color;
            o.Emission.rgb=color; */
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
        
    }
    
}
