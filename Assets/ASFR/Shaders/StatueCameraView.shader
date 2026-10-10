Shader "ASFR/Statue Camera View" {
 Properties {
  _MainTex("Left Eye / Legacy Camera Feed",2D)="black" {}
  _RightTex("Right Eye Feed",2D)="black" {}
  [Toggle] _StereoEnabled("Stereo Cameras",Float)=0
  _TanHalfFov("Camera Projection Scale",Float)=2.747477
 }
 SubShader {
  Tags {"Queue"="Overlay+1000" "RenderType"="Transparent" "DisableBatching"="True" "IgnoreProjector"="True"}
  Cull Off ZWrite On ZTest Always
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   sampler2D _MainTex, _RightTex;
   float4 _MainTex_TexelSize, _RightTex_TexelSize;
   float _StereoEnabled, _TanHalfFov;
   float _VRChatCameraMode,_VRChatMirrorMode,_VRChatFaceMirrorMode;
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   v2f vert(appdata v) {
    v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f,o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.pos=float4(v.vertex.xy,UNITY_NEAR_CLIP_VALUE,1); o.uv=v.uv; return o;
   }
   float4 frag(v2f i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    clip(.5-max(_VRChatCameraMode,max(_VRChatMirrorMode,_VRChatFaceMirrorMode)));
    float2 uv=i.uv;
    bool rightEye=false;
    if (_StereoEnabled>.5) {
     // Reproject each headset eye's asymmetric frustum into the wide square camera.
     // This avoids stretching a square render texture across the headset viewport.
     float4 ray=mul(unity_CameraInvProjection,float4(uv*2-1,1,1));
     uv=.5+.5*ray.xy/(max(abs(ray.z),.00001)*max(_TanHalfFov,.00001));
     #if defined(USING_STEREO_MATRICES)
      rightEye=unity_StereoEyeIndex==1;
     #endif
     if(any(uv<0)||any(uv>1)) return float4(0,0,0,1);
    }
    if(rightEye) {
     #if UNITY_UV_STARTS_AT_TOP
      if(_RightTex_TexelSize.y<0) uv.y=1-uv.y;
     #endif
     return float4(tex2D(_RightTex,uv).rgb,1);
    }
    #if UNITY_UV_STARTS_AT_TOP
     if(_MainTex_TexelSize.y<0) uv.y=1-uv.y;
    #endif
    return float4(tex2D(_MainTex,uv).rgb,1);
   }
   ENDCG
  }
 }
 Fallback Off
}
