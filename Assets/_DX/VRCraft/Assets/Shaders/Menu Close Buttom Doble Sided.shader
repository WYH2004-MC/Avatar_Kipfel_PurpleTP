Shader "DX/VRCraft/Menu Close Buttom Doble Sided"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}

        _MaskTex ("Mask", 2D) = "white" {}

		[Space(50)]
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        _CloseButtomColor ("Close Buttom Color", Color) = (1,1,1,1)

        
        [Header(FallBack)]        
        _Cutoff("Cutoff", Range(0,1)) = .5    
        _Mode ("Mode", Float) = 1.0

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "VRCFallback"="DoubleSidedToonCutout"  }
        LOD 200
        Cull Off

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows addshadow

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex,_MaskTex,_AvatarTex,_BackTex,_EnchantedTex;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_EnchantedTex;
            float IsFacing:VFACE;
        };

        half _Glossiness;
        half _Metallic;
        half _SelectX,_SelectY,_Hand,_Selected,_CloseButtomEnable;
        fixed4 _Color,_MaskColor,_MenuSize,_AvatarSize,_EnchantedSpeed,_CloseButtomColor;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)


        void surf (Input IN, inout SurfaceOutputStandard o)
        {   
            if( _ProjectionParams.y != 0.0113007 && _Time.y>12. ){

                
                fixed4 mask = tex2D (_MaskTex, IN.uv_MainTex ) ;                
                fixed4 c; 
                if( mask.r<.5 ){
                    c = tex2D (_MainTex, IN.uv_MainTex * float2(IN.IsFacing ,1) ) * _Color ;
                }else{
                    c = tex2D (_MainTex, IN.uv_MainTex ) * _Color   ;                  
                }
                
                // Albedo comes from a texture tinted by color
                c.rgb= lerp(c.rgb,_CloseButtomColor.rgb,mask.b*_CloseButtomColor.a);
                
                o.Albedo = c.rgb;
                o.Emission=o.Albedo;
                o.Metallic = _Metallic;
                o.Smoothness = _Glossiness;
                o.Alpha = c.a;
                if(o.Alpha < .8){
                    discard;
                }
            }else{
                discard;
            }
        }
        ENDCG
    }
    FallBack "Diffuse"
}
