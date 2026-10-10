Shader "ASFR/Frozen Vision" {
Properties {
 _Mode("Mode",Float)=0
 _Strength("Strength",Range(0,1))=.45
 _OverlayTex("Custom RGBA Overlay",2D)="black" {}
 _UseTexture("Use Texture",Float)=0
}
SubShader {
 Tags {"Queue"="Overlay+100" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True"}
 Cull Off ZWrite Off ZTest Always
 Blend SrcAlpha OneMinusSrcAlpha
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 sampler2D _OverlayTex;float _Mode,_Strength,_UseTexture;
 float _VRChatCameraMode,_VRChatMirrorMode,_VRChatFaceMirrorMode;
 struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
 v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=float4(v.vertex.xy,UNITY_NEAR_CLIP_VALUE,1);o.uv=v.uv;return o;}
 float2 hash(float2 p){return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453);}
 float4 frag(v2f i):SV_Target {
 UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 clip(.5-max(_VRChatCameraMode,max(_VRChatMirrorMode,_VRChatFaceMirrorMode)));
 float2 uv=i.uv;float edge=smoothstep(.2,.95,length((uv-.5)*1.5));float strength=saturate(_Strength);
 if(_Mode<.5)return float4(0,0,0,strength*.9);
 if(_Mode<1.5){
  float2 p=uv*14,cell=floor(p),f=frac(p);float a=9,b=9;
  for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++){float2 n=float2(x,y);float d=length(n+hash(cell+n)-f);if(d<a){b=a;a=d;}else b=min(b,d);}
  float crack=1-smoothstep(.015,.065,b-a);
  return float4(.65,.88,1,strength*saturate(.12+edge*.5+crack*(.12+.48*edge)));
 }
 float4 tex=tex2D(_OverlayTex,uv);tex.a*=strength*_UseTexture;return tex;
 }
 ENDCG
 }
}
Fallback Off
}

