Shader "DX/VRCraft/QuestTextureCombiner"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
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
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

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
                float2 pixelGrid = i.uv;

                /* if(_MainTex_TexelSize.z>64){
                    pixelGrid=frac(pixelGrid*2);   
                } */
                 
                pixelGrid=frac(pixelGrid*2);   
                
                pixelGrid=floor(pixelGrid*64);
                

                fixed4 col = tex2D(_MainTex, i.uv);

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                float4 texCombine = float4(0,0,0,0);
                if( pixelGrid.x>31 && pixelGrid.y>47 ){

                    texCombine = tex2D(_MainTex, i.uv+float2(-32./128.,0));
                    
                }else if( pixelGrid.x>15 && pixelGrid.y>15 && pixelGrid.x<40 && pixelGrid.y<32 ){

                    texCombine = tex2D(_MainTex, i.uv+float2(0,16./128.));

                }else if( pixelGrid.x<16 && 15 < pixelGrid.y && pixelGrid.y<32 ){

                    texCombine = tex2D(_MainTex, i.uv+float2(0,16./128.));
                }else if( pixelGrid.x<16 && pixelGrid.y<16 ){

                    texCombine = tex2D(_MainTex, i.uv+float2(16./128.,0));
                    
                }else if( 39<pixelGrid.x && pixelGrid.x<56 && 15<pixelGrid.y && pixelGrid.y<32 ){

                    texCombine = tex2D(_MainTex, i.uv+float2(0,16./128.));
                    

                }else if( 47<pixelGrid.x && pixelGrid.y<16 ){

                    texCombine = tex2D(_MainTex, i.uv+float2(-16./128.,0));
                    //texCombine = float4(0,1,0,1);

                }

                col.rgb=lerp(texCombine.rgb,col.rgb,col.a); 
                //col.rgb=  texCombine.rgb; 
                col.a= min(col.a+texCombine.a,1);
                //col.a=1;
                
                //col.rgb = lerp(float3(1,0,1),col.rgb,col.a);
                //col.rgb=float3(pixelGrid/_MainTex_TexelSize.zw,0);
                return col;
            }
            ENDCG
        }
    }
}
