#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    public class UnebetaInspector : lilToonInspector
    {
        // Custom properties
        MaterialProperty _Mask;

        MaterialProperty _MinimumDistance;
        MaterialProperty _BehindDistance;
        MaterialProperty _FlippingDistanceCoefficient;

        MaterialProperty _UneunePower;
        MaterialProperty _UneuneSpeed;
        MaterialProperty _UneunePosition;

        MaterialProperty _useUv0;
        MaterialProperty _useUv1;
        MaterialProperty _useUv2;
        MaterialProperty _useUv3;
        MaterialProperty _useUvMain;
        MaterialProperty _useUvMat;
        MaterialProperty _useUvRim;
        MaterialProperty _useUvPanorama;
        MaterialProperty _useUvSc;
        
        MaterialProperty _useVertex;

        private static bool isShowCustomProperties;
        private const string shaderName = "unebeta";

        protected override void LoadCustomProperties(MaterialProperty[] props, Material material)
        {
            isCustomShader = true;

            // If you want to change rendering modes in the editor, specify the shader here
            ReplaceToCustomShaders();
            isShowRenderMode = !material.shader.name.Contains("Optional");

            // If not, set isShowRenderMode to false
            //isShowRenderMode = false;

            //LoadCustomLanguage("");
            _Mask = FindProperty("_Mask", props);

            _MinimumDistance = FindProperty("_MinimumDistance", props);
            _BehindDistance = FindProperty("_BehindDistance", props);
            _FlippingDistanceCoefficient = FindProperty("_FlippingDistanceCoefficient", props);

            _UneunePower = FindProperty("_UneunePower", props);
            _UneuneSpeed = FindProperty("_UneuneSpeed", props);
            _UneunePosition = FindProperty("_UneunePosition", props);

            _useVertex = FindProperty("_useVertex", props);
            _useUv0 = FindProperty("_useUv0", props);
            _useUv1 = FindProperty("_useUv1", props);
            _useUv2 = FindProperty("_useUv2", props);
            _useUv3 = FindProperty("_useUv3", props);
            _useUvMain = FindProperty("_useUvMain", props);
            _useUvMat = FindProperty("_useUvMat", props);
            _useUvRim = FindProperty("_useUvRim", props);
            _useUvPanorama = FindProperty("_useUvPanorama", props);
            _useUvSc = FindProperty("_useUvSc", props);
        }

        protected override void DrawCustomProperties(Material material)
        {
            // GUIStyles Name   Description
            // ---------------- ------------------------------------
            // boxOuter         outer box
            // boxInnerHalf     inner box
            // boxInner         inner box without label
            // customBox        box (similar to unity default box)
            // customToggleFont label for box

            isShowCustomProperties = Foldout("Custom Properties", "Custom Properties", isShowCustomProperties);
            if (isShowCustomProperties)
            {
                EditorGUILayout.BeginVertical(boxOuter);
                EditorGUILayout.LabelField(GetLoc("Custom Properties"), customToggleFont);
                EditorGUILayout.BeginVertical(boxInnerHalf);

                m_MaterialEditor.ShaderProperty(_Mask, "Mask(範囲指定マスク)");

                m_MaterialEditor.ShaderProperty(_UneunePower, "うねうねパワー");
                m_MaterialEditor.ShaderProperty(_UneuneSpeed, "うねうね速度");
                m_MaterialEditor.ShaderProperty(_UneunePosition, "うねうねのサイズ調整");

                m_MaterialEditor.ShaderProperty(_BehindDistance, "貫通抑止の最大距離");

                EditorGUILayout.LabelField("以下の設定値を変更することで、うねうねの影響範囲を指定できます", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                _useVertex.floatValue = EditorGUILayout.Toggle("頂点で使用する", _useVertex.floatValue == 1.0f) ? 1.0f : 0.0f;
                _useUv0.floatValue = EditorGUILayout.Toggle("UV0で使用する", _useUv0.floatValue == 1.0f) ? 1.0f : 0.0f;
                _useUv1.floatValue = EditorGUILayout.Toggle("UV1で使用する", _useUv1.floatValue == 1.0f) ? 1.0f : 0.0f;
                _useUv2.floatValue = EditorGUILayout.Toggle("UV2で使用する", _useUv2.floatValue == 1.0f) ? 1.0f : 0.0f;
                _useUv3.floatValue = EditorGUILayout.Toggle("UV3で使用する", _useUv3.floatValue == 1.0f) ? 1.0f : 0.0f;
                _useUvMain.floatValue =
                    EditorGUILayout.Toggle("メインテクスチャで使用する", _useUvMain.floatValue == 1.0f) ? 1.0f : 0.0f;
                _useUvMat.floatValue = EditorGUILayout.Toggle("マットキャップテクスチャで使用する", _useUvMat.floatValue == 1.0f)
                    ? 1.0f
                    : 0.0f;
                _useUvRim.floatValue = EditorGUILayout.Toggle("リムライトテクスチャで使用する", _useUvRim.floatValue == 1.0f)
                    ? 1.0f
                    : 0.0f;
                _useUvPanorama.floatValue = EditorGUILayout.Toggle("パノラマテクスチャで使用する", _useUvPanorama.floatValue == 1.0f)
                    ? 1.0f
                    : 0.0f;
                _useUvSc.floatValue =
                    EditorGUILayout.Toggle("スクロールテクスチャで使用する", _useUvSc.floatValue == 1.0f) ? 1.0f : 0.0f;
                EditorGUI.indentLevel--;

                EditorGUILayout.LabelField("以下の設定値は変更すると正しい見た目にならない可能性があるため、気をつけて！", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                m_MaterialEditor.ShaderProperty(_MinimumDistance, "抑止対象頂点との最小距離");
                m_MaterialEditor.ShaderProperty(_FlippingDistanceCoefficient, "抑止時の頂点の移動距離にかける係数");
                EditorGUI.indentLevel--;


                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();
            }
        }

        protected override void ReplaceToCustomShaders()
        {
            lts = Shader.Find(shaderName + "/lilToon");
            ltsc = Shader.Find("Hidden/" + shaderName + "/Cutout");
            ltst = Shader.Find("Hidden/" + shaderName + "/Transparent");
            ltsot = Shader.Find("Hidden/" + shaderName + "/OnePassTransparent");
            ltstt = Shader.Find("Hidden/" + shaderName + "/TwoPassTransparent");

            ltso = Shader.Find("Hidden/" + shaderName + "/OpaqueOutline");
            ltsco = Shader.Find("Hidden/" + shaderName + "/CutoutOutline");
            ltsto = Shader.Find("Hidden/" + shaderName + "/TransparentOutline");
            ltsoto = Shader.Find("Hidden/" + shaderName + "/OnePassTransparentOutline");
            ltstto = Shader.Find("Hidden/" + shaderName + "/TwoPassTransparentOutline");

            ltsoo = Shader.Find(shaderName + "/[Optional] OutlineOnly/Opaque");
            ltscoo = Shader.Find(shaderName + "/[Optional] OutlineOnly/Cutout");
            ltstoo = Shader.Find(shaderName + "/[Optional] OutlineOnly/Transparent");

            ltstess = Shader.Find("Hidden/" + shaderName + "/Tessellation/Opaque");
            ltstessc = Shader.Find("Hidden/" + shaderName + "/Tessellation/Cutout");
            ltstesst = Shader.Find("Hidden/" + shaderName + "/Tessellation/Transparent");
            ltstessot = Shader.Find("Hidden/" + shaderName + "/Tessellation/OnePassTransparent");
            ltstesstt = Shader.Find("Hidden/" + shaderName + "/Tessellation/TwoPassTransparent");

            ltstesso = Shader.Find("Hidden/" + shaderName + "/Tessellation/OpaqueOutline");
            ltstessco = Shader.Find("Hidden/" + shaderName + "/Tessellation/CutoutOutline");
            ltstessto = Shader.Find("Hidden/" + shaderName + "/Tessellation/TransparentOutline");
            ltstessoto = Shader.Find("Hidden/" + shaderName + "/Tessellation/OnePassTransparentOutline");
            ltstesstto = Shader.Find("Hidden/" + shaderName + "/Tessellation/TwoPassTransparentOutline");

            ltsl = Shader.Find(shaderName + "/lilToonLite");
            ltslc = Shader.Find("Hidden/" + shaderName + "/Lite/Cutout");
            ltslt = Shader.Find("Hidden/" + shaderName + "/Lite/Transparent");
            ltslot = Shader.Find("Hidden/" + shaderName + "/Lite/OnePassTransparent");
            ltsltt = Shader.Find("Hidden/" + shaderName + "/Lite/TwoPassTransparent");

            ltslo = Shader.Find("Hidden/" + shaderName + "/Lite/OpaqueOutline");
            ltslco = Shader.Find("Hidden/" + shaderName + "/Lite/CutoutOutline");
            ltslto = Shader.Find("Hidden/" + shaderName + "/Lite/TransparentOutline");
            ltsloto = Shader.Find("Hidden/" + shaderName + "/Lite/OnePassTransparentOutline");
            ltsltto = Shader.Find("Hidden/" + shaderName + "/Lite/TwoPassTransparentOutline");

            ltsref = Shader.Find("Hidden/" + shaderName + "/Refraction");
            ltsrefb = Shader.Find("Hidden/" + shaderName + "/RefractionBlur");
            ltsfur = Shader.Find("Hidden/" + shaderName + "/Fur");
            ltsfurc = Shader.Find("Hidden/" + shaderName + "/FurCutout");
            ltsfurtwo = Shader.Find("Hidden/" + shaderName + "/FurTwoPass");
            ltsfuro = Shader.Find(shaderName + "/[Optional] FurOnly/Transparent");
            ltsfuroc = Shader.Find(shaderName + "/[Optional] FurOnly/Cutout");
            ltsfurotwo = Shader.Find(shaderName + "/[Optional] FurOnly/TwoPass");
            ltsgem = Shader.Find("Hidden/" + shaderName + "/Gem");
            ltsfs = Shader.Find(shaderName + "/[Optional] FakeShadow");

            ltsover = Shader.Find(shaderName + "/[Optional] Overlay");
            ltsoover = Shader.Find(shaderName + "/[Optional] OverlayOnePass");
            ltslover = Shader.Find(shaderName + "/[Optional] LiteOverlay");
            ltsloover = Shader.Find(shaderName + "/[Optional] LiteOverlayOnePass");

            ltsm = Shader.Find(shaderName + "/lilToonMulti");
            ltsmo = Shader.Find("Hidden/" + shaderName + "/MultiOutline");
            ltsmref = Shader.Find("Hidden/" + shaderName + "/MultiRefraction");
            ltsmfur = Shader.Find("Hidden/" + shaderName + "/MultiFur");
            ltsmgem = Shader.Find("Hidden/" + shaderName + "/MultiGem");
        }

        // You can create a menu like this
        /*
        [MenuItem("Assets/TemplateFull/Convert material to custom shader", false, 1100)]
        private static void ConvertMaterialToCustomShaderMenu()
        {
            if(Selection.objects.Length == 0) return;
            TemplateFullInspector inspector = new TemplateFullInspector();
            for(int i = 0; i < Selection.objects.Length; i++)
            {
                if(Selection.objects[i] is Material)
                {
                    inspector.ConvertMaterialToCustomShader((Material)Selection.objects[i]);
                }
            }
        }
        */
    }
}
#endif