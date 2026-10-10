using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ASFR
{
    // Shared geometry and renderer setup for local fullscreen overlays.
    static class ASFRScreen
    {
        public static Mesh CreateQuad(string name, string assetPath)
        {
            var mesh = new Mesh{name = name};
            mesh.vertices = new[]{new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0)};
            mesh.uv = new[]{Vector2.zero, Vector2.right, Vector2.up, Vector2.one};
            mesh.triangles = new[]{0, 2, 1, 2, 3, 1};
            // Vertex shader projects to clip space; bounds keep the head-following quad submitted.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10);
            AssetDatabase.CreateAsset(mesh, assetPath);
            return mesh;
        }

        public static MeshRenderer AddRenderer(GameObject target, Material material)
        {
            target.layer = 10; // PlayerLocal: also excluded from the statue capture camera.
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }
    }
}
