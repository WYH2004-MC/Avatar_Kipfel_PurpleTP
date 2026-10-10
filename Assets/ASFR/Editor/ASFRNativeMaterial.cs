using UnityEditor;
using UnityEngine;

namespace ASFR
{
    // Resolve optimized copies back to the user's native shader without changing source assets.
    static class ASFRNativeMaterial
    {
        public static Shader NativeShader(Material material, bool repairIncludes = false)
        {
            if (!material)
                return null;
            string guid = material.GetTag("OriginalShaderGUID", false, "");
            string name = material.GetTag("OriginalShader", false, "");
            var shader = string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
            if (!shader && !string.IsNullOrEmpty(name))
                shader = Shader.Find(name);
            var resolved = shader ? shader : material.shader;
            if (repairIncludes)
                ASFRShaderIncludes.Repair(resolved);
            return resolved;
        }
    }
}
