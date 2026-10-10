using System.Linq;
using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace ASFR
{
    static class ASFROverlayMaterial
    {
        public static bool Supported(Material material)
        {
            var shader = ASFRNativeMaterial.NativeShader(material);
            if (shader && IsLil(shader))
            {
                var check = new Material(shader);
                bool result = check.HasProperty("_DissolveParams") && check.HasProperty("_DissolvePos");
                UnityEngine.Object.DestroyImmediate(check);
                return result;
            }

            if (!shader || shader.name.IndexOf("poiyomi", StringComparison.OrdinalIgnoreCase) < 0 || shader.name.StartsWith("Hidden/"))
                return false;
            var test = new Material(shader);
            bool supported = test.HasProperty("_DissolveP2PClamp") && test.HasProperty("_AlphaForceOpaque") && test.HasProperty("_Cutoff");
            UnityEngine.Object.DestroyImmediate(test);
            return supported;
        }

        static void Float(Material m, string p, float value)
        {
            if (m.HasProperty(p))
                m.SetFloat(p, value);
        }

        static bool IsLil(Shader shader)
        {
            // Full ordinary lilToon variants; special fur/gem/lite/multi shaders need separate handling.
            return new[]{"lilToon", "Hidden/lilToonOutline", "Hidden/lilToonCutout", "Hidden/lilToonCutoutOutline", "Hidden/lilToonTransparent", "Hidden/lilToonTransparentOutline", "Hidden/lilToonOnePassTransparent", "Hidden/lilToonOnePassTransparentOutline", "Hidden/lilToonTwoPassTransparent", "Hidden/lilToonTwoPassTransparentOutline"}.Contains(shader.name);
        }

        public static void Reveal(Material material, Transform renderer, Transform root, float bottom, float top, out string property, out float start, out float end)
        {
            if (IsLil(material.shader))
            {
                property = "material._DissolveParams.z";
                start = -bottom;
                end = -top;
            }
            else
            {
                property = "material._DissolveAlpha";
                start = 1;
                end = 0;
            }
        }

        // Key complete vectors: a partial material-property-block override can lose
        // the unanimated mode, shape or direction components.
        public static void AnimateLil(AnimationClip clip, Transform renderer, Transform root, float from, float to, float duration, bool removal, bool completed = false)
        {
            string path = AnimationUtility.CalculateTransformPath(renderer, root);
            void Set(string property, float a, float b)
            {
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), "material." + property), AnimationCurve.Linear(0, a, duration, b));
            }

            // Once revealed, stop clipping by an installation-time height plane.
            float mode = completed && !removal ? 0 : 3;
            Set("_DissolveParams.x", mode, mode);
            Set("_DissolveParams.y", 1, 1);
            Set("_DissolveParams.z", from, to);
            Set("_DissolveParams.w", .001f, .001f);
            Set("_DissolvePos.x", 0, 0);
            Set("_DissolvePos.y", removal ? 1 : -1, removal ? 1 : -1);
            Set("_DissolvePos.z", 0, 0);
            Set("_DissolvePos.w", 0, 0);
        }

        public static Material Create(Material source, Transform renderer, Transform root, float bottom, float top, int sourceQueue)
        {
            var material = new Material(source)
            {name = source.name + " ASFR Overlay"};
            material.shader = ASFRNativeMaterial.NativeShader(source, true);
            // A second outline hull around an expanded face produces doubled mouth edges.
            // Keep outlines on the original avatar, but not on generated coating shells.
            Float(material, "_EnableOutlines", 0);
            Float(material, "_OutlineWidth", 0);
            Float(material, "_OutlineWidth2", 0);
            if (IsLil(material.shader))
            {
                if (material.shader.name == "lilToon" || material.shader.name == "Hidden/lilToonOutline")
                {
                    string variant = material.shader.name.EndsWith("Outline") ? "Hidden/lilToonCutoutOutline" : "Hidden/lilToonCutout";
                    var shader = Shader.Find(variant);
                    if (!shader)
                        throw new InvalidOperationException("Missing lilToon cutout variant: " + variant);
                    material.shader = shader;
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                }

                material.SetVector("_DissolvePos", new Vector4(0, -1, 0, 0));
                material.SetVector("_DissolveParams", new Vector4(3, 1, -bottom, .001f));
                material.SetColor("_DissolveColor", Color.clear);
                Float(material, "_DissolveNoiseStrength", 0);
                Float(material, "_Cutoff", Mathf.Max(.001f, material.GetFloat("_Cutoff")));
                Float(material, "_ZTest", 4);
                material.renderQueue = Mathf.Clamp(Mathf.Max(material.renderQueue, sourceQueue + 1), 2451, 4999);
                return material;
            }

            material.SetOverrideTag("OriginalShader", "");
            material.SetOverrideTag("OriginalShaderGUID", "");
            Float(material, "_ShaderOptimizerEnabled", 0);
            // Preserve all authored surface effects. Only configure visibility/reveal on this copy.
            for (int i = 0; i < material.shader.GetPropertyCount(); i++)
                foreach (var attribute in material.shader.GetPropertyAttributes(i))
                {
                    var match = Regex.Match(attribute, @"^(?:ThryToggle|Toggle)\((\w+)(?:,\s*\w+)?\)$");
                    if (!match.Success)
                        continue;
                    if (material.GetFloat(material.shader.GetPropertyName(i)) != 0)
                        material.EnableKeyword(match.Groups[1].Value);
                    else
                        material.DisableKeyword(match.Groups[1].Value);
                }

            ASFRDissolveAlphaFix.Configure(material);
            Float(material, "_EnableDissolve", 1);
            material.EnableKeyword("DISTORT");
            // Overlays have been rebased into the same avatar-root frame.
            Float(material, "_DissolveType", 2);
            Float(material, "_DissolveP2PWorldLocal", 0);
            Float(material, "_DissolveP2PClamp", 1);
            material.SetVector("_DissolveStartPoint", new Vector4(0, top, 0, 0));
            material.SetVector("_DissolveEndPoint", new Vector4(0, bottom, 0, 0));
            material.SetColor("_DissolveTextureColor", Color.clear);
            material.SetTexture("_DissolveToTexture", Texture2D.whiteTexture);
            material.SetTexture("_DissolveMask", Texture2D.whiteTexture);
            foreach (var p in new[]{"_ContinuousDissolve", "_DissolveDetailStrength", "_DissolveEdgeWidth", "_DissolveP2PEdgeLength", "_DissolveEdgeEmission", "_DissolveToEmissionStrength", "_DissolveHueShiftEnabled", "_DissolveTextureColorThemeIndex", "_EnableDissolveAudioLink", "_DissolveUseVertexColors", "_DissolveMaskInvert", "_DissolveMaskGlobalMask", "_DissolveApplyGlobalMaskIndex", "_DissolveInverseApplyGlobalMaskIndex", "_UVTileDissolveEnabled", "_AlphaForceOpaque", "_AlphaForceOpaque2"})
                Float(material, p, 0);
            for (int i = 0; i < 10; i++)
                Float(material, "_DissolveAlpha" + i, 0);
            Float(material, "_DissolveAlpha", 1);
            material.SetOverrideTag("_DissolveAlphaAnimated", "1");
            // Clip fully dissolved pixels even for an opaque source; keep its blend/ZWrite settings.
            Float(material, "_Cutoff", Mathf.Max(.001f, material.GetFloat("_Cutoff")));
            Float(material, "_ZTest", 4); // Normal occlusion by scene geometry.
            material.renderQueue = Mathf.Clamp(Mathf.Max(material.renderQueue, sourceQueue + 1), 2001, 4999);
            return material;
        }
    }
}
