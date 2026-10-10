using System;
using System.Collections.Generic;
using UnityEngine;

namespace ASFR
{
    static class ASFRPlanarMesh
    {
        // Rebase mesh data and inverse bind matrices together: the world-space skin stays identical.
        public static void Rebase(Mesh mesh, Matrix4x4 matrix)
        {
            if (Mathf.Abs(matrix.determinant) < 1e-10f)
                throw new InvalidOperationException("Cannot rebase a mesh with zero scale.");
            var normalMatrix = matrix.inverse.transpose;
            var frames = new List<Tuple<string, float, Vector3[], Vector3[], Vector3[]>>();
            for (int shape = 0; shape < mesh.blendShapeCount; shape++)
                for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(shape); frame++)
                {
                    var v = new Vector3[mesh.vertexCount];
                    var n = new Vector3[v.Length];
                    var t = new Vector3[v.Length];
                    mesh.GetBlendShapeFrameVertices(shape, frame, v, n, t);
                    for (int i = 0; i < v.Length; i++)
                    {
                        v[i] = matrix.MultiplyVector(v[i]);
                        n[i] = normalMatrix.MultiplyVector(n[i]);
                        t[i] = matrix.MultiplyVector(t[i]);
                    }

                    frames.Add(Tuple.Create(mesh.GetBlendShapeName(shape), mesh.GetBlendShapeFrameWeight(shape, frame), v, n, t));
                }

            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            for (int i = 0; i < normals.Length; i++)
                normals[i] = normalMatrix.MultiplyVector(normals[i]);
            float sign = matrix.determinant < 0 ? -1 : 1;
            for (int i = 0; i < tangents.Length; i++)
            {
                var t = matrix.MultiplyVector(tangents[i]);
                tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w * sign);
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            var binds = mesh.bindposes;
            for (int i = 0; i < binds.Length; i++)
                binds[i] = binds[i] * matrix.inverse;
            mesh.bindposes = binds;
            mesh.ClearBlendShapes();
            foreach (var frame in frames)
                mesh.AddBlendShapeFrame(frame.Item1, frame.Item2, frame.Item3, frame.Item4, frame.Item5);
            if (sign < 0)
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    if (mesh.GetTopology(sub) == MeshTopology.Triangles)
                    {
                        var tris = mesh.GetTriangles(sub);
                        for (int i = 0; i < tris.Length; i += 3)
                        {
                            int temp = tris[i];
                            tris[i] = tris[i + 1];
                            tris[i + 1] = temp;
                        }

                        mesh.SetTriangles(tris, sub);
                    }

            mesh.RecalculateBounds();
        }

        // Visibility-only hierarchy: shares the avatar frame without inheriting limb motion.
        public static Transform Gate(Transform source, Transform root, Transform parent, Dictionary<Transform, Transform> map)
        {
            if (source == root)
                return parent;
            if (map.TryGetValue(source, out var existing))
                return existing;
            var container = Gate(source.parent, root, parent, map);
            var gate = new GameObject("Visibility_" + map.Count.ToString("D4")).transform;
            gate.SetParent(container, false);
            gate.gameObject.SetActive(source.gameObject.activeSelf);
            map.Add(source, gate);
            return gate;
        }
    }
}
