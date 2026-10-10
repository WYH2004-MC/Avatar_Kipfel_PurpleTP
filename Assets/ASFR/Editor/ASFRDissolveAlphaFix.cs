using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ASFR
{
    static class ASFRDissolveAlphaFix
    {
        public static void Configure(Material material)
        {
            if (!material || !material.shader || material.shader.name.IndexOf("poiyomi", StringComparison.OrdinalIgnoreCase) < 0)
                return;
            // The outline pass forces alpha to one in Opaque mode, after applying dissolve.
            if (material.HasProperty("_Mode") && Mathf.Approximately(material.GetFloat("_Mode"), 0))
            {
                material.SetFloat("_Mode", 1);
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = Mathf.Max(2450, material.renderQueue);
            }

            foreach (string property in new[]{"_OutlineOverrideAlpha", "_AlphaForceOpaque", "_AlphaForceOpaque2"})
                if (material.HasProperty(property))
                    material.SetFloat(property, 0);
            if (material.HasProperty("_Cutoff"))
                material.SetFloat("_Cutoff", Mathf.Max(.001f, material.GetFloat("_Cutoff")));
        }

        [MenuItem("Tools/ASFR/Misc/Repair generated dissolve outlines", false, 110)]
        static void Repair()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ASFRGenerated"))
                return;
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[]{"Assets/ASFRGenerated"}))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileName(path);
                if (!name.StartsWith("ClothingMaterial_", StringComparison.Ordinal) && !name.StartsWith("Overlay_", StringComparison.Ordinal))
                    continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material || !material.shader || material.shader.name.IndexOf("poiyomi", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                Undo.RecordObject(material, "Repair ASFR dissolve outlines");
                Configure(material);
                EditorUtility.SetDirty(material);
                count++;
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("ASFR", "Updated " + count + " generated Poiyomi materials. Test the transformation again. Original clothing material assets were not changed.", "OK");
        }

        [MenuItem("Tools/ASFR/Misc/Repair generated dissolve outlines", true)]
        static bool CanRepair()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
