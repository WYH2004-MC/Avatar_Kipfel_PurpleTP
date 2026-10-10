using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ASFR
{
    static class ASFRClothingDissolve
    {
        public static SkinnedMeshRenderer[] Create(Transform root, Transform system, SkinnedMeshRenderer[] sources, Dictionary<Transform, Transform> visibility, string folder, float bottom, float top)
        {
            var result = new List<SkinnedMeshRenderer>();
            foreach (var source in sources)
            {
                foreach (var material in source.sharedMaterials)
                    if (material && !ASFROverlayMaterial.Supported(material))
                        throw new InvalidOperationException("Progressive clothing removal needs a supported Poiyomi or full lilToon material. Clothing: " + source.name + ", material: " + material.name + ". Unmark this mesh as clothing to exclude it.");
                if (!source.sharedMesh.isReadable)
                    throw new InvalidOperationException("Enable mesh Read/Write for clothing: " + source.name);
                var go = new GameObject("ASFR Clothing Dissolve");
                go.transform.SetParent(ASFRPlanarMesh.Gate(source.transform, root, system, visibility), false);
                var copy = go.AddComponent<SkinnedMeshRenderer>();
                EditorUtility.CopySerialized(source, copy);
                var mesh = UnityEngine.Object.Instantiate(source.sharedMesh);
                mesh.name = source.sharedMesh.name + " Clothing Dissolve";
                ASFRPlanarMesh.Rebase(mesh, root.worldToLocalMatrix * source.transform.localToWorldMatrix);
                AssetDatabase.CreateAsset(mesh, folder + "/ClothingMesh_" + result.Count + ".asset");
                copy.sharedMesh = mesh;
                copy.rootBone = root;
                copy.localBounds = mesh.bounds;
                copy.updateWhenOffscreen = true;
                var materials = source.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                    if (materials[slot])
                    {
                        var material = ASFROverlayMaterial.Create(materials[slot], copy.transform, root, bottom, top, materials[slot].renderQueue);
                        if (material.HasProperty("_DissolveParams"))
                        {
                            material.SetVector("_DissolvePos", new Vector4(0, 1, 0, 0));
                            material.SetVector("_DissolveParams", new Vector4(3, 1, bottom, .001f));
                        }
                        else
                        {
                            material.SetVector("_DissolveStartPoint", new Vector4(0, bottom, 0, 0));
                            material.SetVector("_DissolveEndPoint", new Vector4(0, top, 0, 0));
                            material.SetFloat("_DissolveAlpha", 0);
                        }

                        AssetDatabase.CreateAsset(material, folder + "/ClothingMaterial_" + result.Count + "_" + slot + ".mat");
                        materials[slot] = material;
                    }

                copy.sharedMaterials = materials;
                go.SetActive(false);
                result.Add(copy);
            }

            return result.ToArray();
        }

        public static void Animate(AnimationClip clip, Transform root, SkinnedMeshRenderer[] copies, float bottom, float top, float seconds, bool start, bool end)
        {
            foreach (var copy in copies)
                foreach (bool lil in copy.sharedMaterials.Where(m => m).Select(m => m.HasProperty("_DissolveParams")).Distinct())
                {
                    string property = lil ? "material._DissolveParams.z" : "material._DissolveAlpha";
                    float from = lil ? (start ? top : bottom) : (start ? 1 : 0), to = lil ? (end ? top : bottom) : (end ? 1 : 0);
                    if (lil)
                        ASFROverlayMaterial.AnimateLil(clip, copy.transform, root, from, to, seconds, true);
                    else
                    {
                        var binding = EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(copy.transform, root), typeof(SkinnedMeshRenderer), property);
                        AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Linear(0, from, seconds, to));
                    }
                }
        }
    }
}
