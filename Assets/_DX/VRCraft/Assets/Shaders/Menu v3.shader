Shader "DX/VRCraft/MenuV3"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
		[Space(50)]
        _MaskColor ("Color", Color) = (1,1,1,1)
        _MaskTex ("Mask", 2D) = "white" {}
		[Space(50)]
        _AvatarTex ("Avatar", 2D) = "Black" {}
        _BackTex ("Back Texure", 2D) = "Black" {}
		[Space(50)]
        _EnchantedTex ("Enchanted Texure", 2D) = "Black" {}
        _EnchantedSpeed ("Enchanted Speed", Vector) = (0.04, 0.04, 0.502, 0.09)
		[Space(50)]
        _CloseButtomColor ("Close Buttom Color", Color) = (1,1,1,1)
		[MaterialToggle] _CloseButtomEnable ("Close Buttom Enable", Float ) = 0
		[Space(50)]
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        _MenuSize ("Menu Size", Vector) = (0.04, 0.04, 0.502, 0.09)
        _AvatarSize ("Avatar Size", Vector) = (0.04, 0.04, 0.502, 0.09)


        _Selected ("Selected", Range(0,36)) = 0.0
        _Hand ("Hand", Range(0,36)) = 0.0

        
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
            if( _ProjectionParams.y != 0.5113007  ){
            
                // Albedo comes from a texture tinted by color
                fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
                if(IN.IsFacing > 0 ){

                    
                    float3 mask = tex2D(_MaskTex, IN.uv_MainTex);      
                    

                    float3 EnchantedTex = tex2D(_EnchantedTex , IN.uv_EnchantedTex+_EnchantedSpeed.xy*_Time.y).xyz;//IN.uv_EnchantedMaskTex+_EnchantedSpeed.xy*_Time.y
                    
                    c.rgb+=mask.y*EnchantedTex;
                    
                    o.Albedo = c.rgb;

                    if( _CloseButtomEnable > 0 ){
                        o.Albedo =(1-_CloseButtomColor.a*mask.z)*o.Albedo+_CloseButtomColor.rgb*(_CloseButtomColor.a*mask.z);
                    }     

                    // Metallic and smoothness come from slider variables
                    float2 grid = clamp((IN.uv_MainTex.xy-_MenuSize.xy)/(float2(1.0,1.0)-_MenuSize.xy-_MenuSize.zw),0,1);
                    float gridID = floor(grid.x*9.0)+floor(grid.y*4.0)*9.0;

                    if( floor(_Hand) == gridID){
                        o.Albedo = 0.272549019*mask.x+o.Albedo*(1-mask.x);
                    }

 
                    if( floor(_Selected) == gridID ){
                        o.Albedo =(1-_MaskColor.a*mask.x)*o.Albedo+_MaskColor.rgb*(_MaskColor.a*mask.x);
                    }


                    float2 avatarUV= (IN.uv_MainTex.xy-_AvatarSize.xy)/(_AvatarSize.zw-_AvatarSize.xy);
                    
                    if( 0.0<avatarUV.x && avatarUV.x<1.0 && 0.0<avatarUV.y && avatarUV.y<1.0 ){
                        o.Albedo =tex2D (_AvatarTex, float2(1-avatarUV.x,avatarUV.y)) * _Color;
                    }
                    
                    
                    
                }else{                    
                    
                    o.Albedo =  tex2D (_BackTex, IN.uv_MainTex*float2(-1,1)) * _Color;
                }
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
